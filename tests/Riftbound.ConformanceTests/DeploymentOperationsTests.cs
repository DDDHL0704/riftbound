using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Riftbound.Api;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class DeploymentOperationsTests
{
    [Fact]
    public void OperationalStatusReportsConfiguredModesWithoutExposingSecrets()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Riftbound"] = "Host=db;Database=riftbound;Username=riftbound;Password=postgres-secret",
                ["Riftbound:SignalR:Redis:ConnectionString"] = "redis:6379,password=redis-secret",
                ["Riftbound:DevUiOrigins:0"] = "https://play.example.test",
                ["Riftbound:Metrics:Enabled"] = "true"
            })
            .Build();

        var status = RiftboundOperationalStatus.Build(configuration, new TestHostEnvironment("Production"));
        var serialized = JsonSerializer.Serialize(status);

        Assert.Equal("riftbound-dotnet", status.Service);
        Assert.Equal("postgres", status.PersistenceMode);
        Assert.Equal("redis-backplane", status.SignalRScaleMode);
        Assert.True(status.RedisBackplaneConfigured);
        Assert.Equal(1, status.ConfiguredCorsOriginCount);
        Assert.DoesNotContain("postgres-secret", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("redis-secret", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password=", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password=", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OperationalMetricsUseSanitizedLowCardinalityLabels()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Riftbound"] = "",
                ["ConnectionStrings:SignalRRedis"] = "redis:6379,password=redis-secret",
                ["Riftbound:Metrics:Enabled"] = "true"
            })
            .Build();

        var status = RiftboundOperationalStatus.Build(configuration, new TestHostEnvironment("Production"));
        var metrics = RiftboundOperationalStatus.ToPrometheus(status);

        Assert.Contains("riftbound_health_status", metrics, StringComparison.Ordinal);
        Assert.Contains("persistence=\"memory\"", metrics, StringComparison.Ordinal);
        Assert.Contains("signalr_scale=\"redis-backplane\"", metrics, StringComparison.Ordinal);
        Assert.DoesNotContain("redis-secret", metrics, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password=", metrics, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DeploymentArtifactsDocumentContainerConfigHealthMetricsAndBackplane()
    {
        var root = FindRepositoryRoot();
        var dockerfile = File.ReadAllText(Path.Combine(root, "Dockerfile"));
        var dockerignore = File.ReadAllText(Path.Combine(root, ".dockerignore"));
        var envExample = File.ReadAllText(Path.Combine(root, ".env.example"));
        var deployment = File.ReadAllText(Path.Combine(root, "docs", "DEPLOYMENT.md"));

        Assert.Contains("src/Riftbound.DevUi", dockerfile, StringComparison.Ordinal);
        Assert.Contains("npm run build", dockerfile, StringComparison.Ordinal);
        Assert.Contains("dotnet publish", dockerfile, StringComparison.Ordinal);
        Assert.Contains("/health", dockerfile, StringComparison.Ordinal);
        Assert.Contains("8080", dockerfile, StringComparison.Ordinal);

        Assert.Contains("src/Riftbound.DevUi/node_modules", dockerignore, StringComparison.Ordinal);
        Assert.Contains("**/bin", dockerignore, StringComparison.Ordinal);
        Assert.Contains("**/obj", dockerignore, StringComparison.Ordinal);

        Assert.Contains("ConnectionStrings__Riftbound=", envExample, StringComparison.Ordinal);
        Assert.Contains("Riftbound__DevUiOrigins__0=", envExample, StringComparison.Ordinal);
        Assert.Contains("Riftbound__SignalR__Redis__ConnectionString=", envExample, StringComparison.Ordinal);
        Assert.Contains("Riftbound__Metrics__Enabled=true", envExample, StringComparison.Ordinal);
        Assert.DoesNotContain("Password=", envExample, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", envExample, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("docker build", deployment, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("docker run", deployment, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/health", deployment, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/metrics", deployment, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Postgres", deployment, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Redis", deployment, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rollback", deployment, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Riftbound.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Riftbound.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
