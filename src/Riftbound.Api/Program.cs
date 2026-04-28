using Riftbound.Api.Hubs;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Riftbound.Persistence;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR().AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddRiftboundPersistence(builder.Configuration);
builder.Services.AddSingleton<IRuleEngine, CoreRuleEngine>();
builder.Services.AddSingleton<IMatchSessionRegistry, InMemoryMatchSessionRegistry>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "riftbound-dotnet",
    role = "migration-skeleton",
    dotnet = Environment.Version.ToString()
}));

app.MapGet("/catalog/summary", async (CancellationToken cancellationToken) =>
{
    var catalog = await OfficialCardCatalog.LoadDefaultAsync(cancellationToken);
    var units = FunctionalUnitBuilder.Build(catalog.Cards);
    var summary = FunctionalUnitBuilder.Summarize(units);
    return Results.Ok(new
    {
        catalog.Source,
        catalog.FetchedAt,
        catalog.Total,
        loadedCards = catalog.Cards.Count,
        summary.FunctionalUnits,
        summary.DuplicateGroups,
        summary.DuplicateEntries,
        summary.SavedLogicImplementations
    });
});

app.MapHub<GameHub>("/hubs/game");

app.Run();
