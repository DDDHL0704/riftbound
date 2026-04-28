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
            services.AddSingleton<IMatchRecoveryStore>(NoopMatchRecoveryStore.Instance);
            services.AddSingleton<IMatchPlayerStore>(NoopMatchPlayerStore.Instance);
            return services;
        }

        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        services.AddSingleton<IMatchJournal, PostgresMatchJournal>();
        services.AddSingleton<IMatchRecoveryStore, PostgresMatchRecoveryStore>();
        services.AddSingleton<IMatchPlayerStore, PostgresMatchPlayerStore>();
        services.AddHostedService<PostgresSchemaInitializer>();
        return services;
    }
}

internal sealed class PostgresSchemaInitializer(NpgsqlDataSource dataSource) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var schemaDirectory = Path.Combine(AppContext.BaseDirectory, "Sql");
        if (!Directory.Exists(schemaDirectory))
        {
            throw new DirectoryNotFoundException($"Persistence SQL directory is missing: {schemaDirectory}");
        }

        var schemaPaths = Directory.GetFiles(schemaDirectory, "*.sql").Order(StringComparer.Ordinal).ToArray();
        if (schemaPaths.Length == 0)
        {
            throw new FileNotFoundException("Persistence SQL migrations are missing.", schemaDirectory);
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        foreach (var schemaPath in schemaPaths)
        {
            var sql = await File.ReadAllTextAsync(schemaPath, cancellationToken).ConfigureAwait(false);
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
