using Riftbound.Api.Hubs;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Riftbound.Persistence;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

var devUiOrigins = builder.Configuration
    .GetSection("Riftbound:DevUiOrigins")
    .Get<string[]>()
    ?? [
        "http://127.0.0.1:5173",
        "http://localhost:5173",
        "http://127.0.0.1:5174",
        "http://localhost:5174"
    ];

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevUi", policy =>
    {
        policy
            .WithOrigins(devUiOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
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

app.UseCors("DevUi");

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
        ? Results.NotFound(new { cardNo, message = "BehaviorSpec not found." })
        : Results.Ok(spec);
});

app.MapGet("/catalog/keyword-coverage", async (CancellationToken cancellationToken) =>
{
    var catalog = await OfficialCardCatalog.LoadDefaultAsync(cancellationToken);
    var units = FunctionalUnitBuilder.Build(catalog.Cards);
    var specs = BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, ImplementedBehaviors(catalog.Cards));
    return Results.Ok(KeywordCoverageReporter.Build(specs));
});

app.MapHub<GameHub>("/hubs/game");

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
