using Riftbound.Api.Hubs;
using Riftbound.Api;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Riftbound.Persistence;
using Microsoft.Extensions.FileProviders;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

var devUiOrigins = builder.Configuration
    .GetSection("Riftbound:DevUiOrigins")
    .Get<string[]>()
    ?? DevUiCorsPolicy.DefaultOrigins;

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevUi", policy =>
    {
        DevUiCorsPolicy.Apply(policy, devUiOrigins, builder.Environment.IsDevelopment());
    });
});

builder.Services.AddSignalR().AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddRiftboundPersistence(builder.Configuration);
builder.Services.AddSingleton<IRuleEngine, CoreRuleEngine>();
builder.Services.AddSingleton<IMatchSessionRegistry>(services => new InMemoryMatchSessionRegistry(
    services.GetRequiredService<IRuleEngine>(),
    services.GetRequiredService<IMatchJournal>(),
    services.GetRequiredService<IMatchRecoveryStore>(),
    services.GetRequiredService<IMatchPlayerStore>(),
    new MatchSessionOptions(AllowLegacyReadyWithoutDeck: builder.Environment.IsDevelopment())));

var app = builder.Build();

var devUiDistPath = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "../Riftbound.DevUi/dist"));
var devUiDistProvider = Directory.Exists(devUiDistPath)
    ? new PhysicalFileProvider(devUiDistPath)
    : null;

app.UseCors("DevUi");
if (devUiDistProvider is not null)
{
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = devUiDistProvider });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = devUiDistProvider });
}

app.UseDefaultFiles();
app.UseStaticFiles();

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
    var schema = OfficialCardSchemaValidator.Validate(catalog);
    var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));
    var behaviorReport = BehaviorSpecCatalogBuilder.BuildReport(specs);
    var keywordCoverage = KeywordCoverageReporter.Build(specs);
    return Results.Ok(new
    {
        catalog.Source,
        catalog.FetchedAt,
        catalog.Total,
        loadedCards = catalog.Cards.Count,
        summary.FunctionalUnits,
        summary.DuplicateGroups,
        summary.DuplicateEntries,
        summary.SavedLogicImplementations,
        schemaValid = schema.IsValid,
        schemaViolationCount = schema.Violations.Count,
        behaviorStatusCounts = behaviorReport.StatusCounts,
        behaviorConformanceTierCounts = behaviorReport.ConformanceTierCounts,
        keywordCoverage
    });
});

app.MapGet("/catalog/p3-status", async (CancellationToken cancellationToken) =>
{
    var catalog = await OfficialCardCatalog.LoadDefaultAsync(cancellationToken);
    var units = FunctionalUnitBuilder.Build(catalog.Cards);
    var summary = FunctionalUnitBuilder.Summarize(units);
    var schema = OfficialCardSchemaValidator.Validate(catalog);
    var stability = FunctionalUnitReporter.Build(units);
    var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));
    var behaviorReport = BehaviorSpecCatalogBuilder.BuildReport(specs);
    var keywordCoverage = KeywordCoverageReporter.Build(specs);

    return Results.Ok(new
    {
        officialEntries = catalog.Cards.Count,
        catalog.Total,
        schemaValid = schema.IsValid,
        schemaViolationCount = schema.Violations.Count,
        summary.FunctionalUnits,
        stability.IdsAreUnique,
        behaviorReport.BehaviorSpecs,
        behaviorReport.StatusCounts,
        behaviorReport.ConformanceTierCounts,
        behaviorReport.MissingReasonCardNos,
        keywordCoverage
    });
});

app.MapGet("/catalog/behavior-specs", async (string? cardNo, CancellationToken cancellationToken) =>
{
    var catalog = await OfficialCardCatalog.LoadDefaultAsync(cancellationToken);
    var units = FunctionalUnitBuilder.Build(catalog.Cards);
    var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));
    if (string.IsNullOrWhiteSpace(cardNo))
    {
        return Results.Ok(specs);
    }

    var spec = specs.FirstOrDefault(candidate => string.Equals(candidate.CardNo, cardNo.Trim(), StringComparison.Ordinal));
    return spec is null
        ? Results.NotFound(new { cardNo, message = "找不到该卡牌的行为规格。" })
        : Results.Ok(spec);
});

app.MapGet("/catalog/keyword-coverage", async (CancellationToken cancellationToken) =>
{
    var catalog = await OfficialCardCatalog.LoadDefaultAsync(cancellationToken);
    var units = FunctionalUnitBuilder.Build(catalog.Cards);
    var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));
    return Results.Ok(KeywordCoverageReporter.Build(specs));
});

app.MapGet("/decks/preconstructed", async (CancellationToken cancellationToken) =>
{
    var catalog = await OfficialCardCatalog.LoadDefaultAsync(cancellationToken);
    var decks = PreconstructedDeckCatalog.Build(catalog).Select(deck => new
    {
        deck.Id,
        deck.Name,
        deck.Description,
        legendCardNo = deck.Decklist.LegendCardNo,
        championCardNo = deck.Decklist.ChampionCardNo,
        mainDeck = deck.Decklist.MainDeck,
        runeDeck = deck.Decklist.RuneDeck,
        battlefields = deck.Decklist.Battlefields
    });
    return Results.Ok(decks);
});

app.MapHub<GameHub>("/hubs/game");
if (devUiDistProvider is not null)
{
    app.MapFallback(async context =>
    {
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(Path.Combine(devUiDistPath, "index.html"));
    });
}
else
{
    app.MapFallbackToFile("index.html");
}

app.Run();

static IReadOnlyList<ImplementedCardBehavior> ImplementedBehaviors(IReadOnlyList<OfficialCard> cards)
{
    var playCardBehaviors = CardBehaviorRegistry.GetAll()
        .Select(definition => new ImplementedCardBehavior(
            definition.CardNo,
            definition.EffectKind,
            definition.DisplayName))
        .ToArray();

    return OfficialRuleDomainBehaviorCatalog.MergeWithNonPlayCardDomains(cards, playCardBehaviors);
}
