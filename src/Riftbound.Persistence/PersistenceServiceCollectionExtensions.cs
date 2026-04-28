using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Riftbound.Engine;

namespace Riftbound.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddRiftboundPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Riftbound");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddSingleton<IMatchJournal>(NoopMatchJournal.Instance);
            return services;
        }

        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        services.AddSingleton<IMatchJournal, PostgresMatchJournal>();
        services.AddHostedService<PostgresSchemaInitializer>();
        return services;
    }
}

internal sealed class PostgresSchemaInitializer(NpgsqlDataSource dataSource) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "Sql", "001_p1_event_store.sql");
        if (!File.Exists(schemaPath))
        {
            throw new FileNotFoundException("P1 event store schema is missing.", schemaPath);
        }

        var sql = await File.ReadAllTextAsync(schemaPath, cancellationToken).ConfigureAwait(false);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
