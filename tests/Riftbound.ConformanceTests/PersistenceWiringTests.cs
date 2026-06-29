using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Riftbound.Engine;
using Riftbound.Persistence;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class PersistenceWiringTests
{
    [Fact]
    public void AddRiftboundPersistenceFallsBackToNoopWithoutConnectionString()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddRiftboundPersistence(configuration);

        using var provider = services.BuildServiceProvider();
        Assert.Same(NoopMatchJournal.Instance, provider.GetRequiredService<IMatchJournal>());
        Assert.Same(NoopMatchRecoveryStore.Instance, provider.GetRequiredService<IMatchRecoveryStore>());
        Assert.Same(NoopMatchPlayerStore.Instance, provider.GetRequiredService<IMatchPlayerStore>());
        Assert.IsType<InMemoryMatchResultStore>(provider.GetRequiredService<IMatchResultStore>());
    }

    [Fact]
    public void AddRiftboundPersistenceRegistersPostgresStoresWithConnectionString()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Riftbound"] = "Host=localhost;Port=5432;Database=riftbound_test"
            })
            .Build();

        services.AddRiftboundPersistence(configuration);

        // Inspect registrations without resolving so the test never opens a database connection.
        Assert.Equal(typeof(PostgresMatchJournal), DescriptorFor<IMatchJournal>(services).ImplementationType);
        Assert.Equal(typeof(PostgresMatchRecoveryStore), DescriptorFor<IMatchRecoveryStore>(services).ImplementationType);
        Assert.Equal(typeof(PostgresMatchPlayerStore), DescriptorFor<IMatchPlayerStore>(services).ImplementationType);
        Assert.Equal(typeof(PostgresPlayerIdentityStore), DescriptorFor<IPlayerIdentityStore>(services).ImplementationType);
        Assert.Equal(typeof(PostgresMatchResultStore), DescriptorFor<IMatchResultStore>(services).ImplementationType);
    }

    private static ServiceDescriptor DescriptorFor<TService>(IServiceCollection services)
    {
        return services.Single(descriptor => descriptor.ServiceType == typeof(TService));
    }
}
