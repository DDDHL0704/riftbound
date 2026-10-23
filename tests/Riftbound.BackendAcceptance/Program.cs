using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Npgsql;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

if (args.Length == 0 || args[0] is "--help" or "help")
{
    Console.WriteLine("Usage: Riftbound.BackendAcceptance <payment-resource-surplus|damage-doubling-combat-keywords|ven-flow-combat-keywords|optional-additional-cost-reductions|instruction-power-payment|restricted-mana-lux|empowered-target-action-replacement> <sql|prepare|play|recover|finish|verify|tamper> <output> [before-restart|after-restart|final]");
    Console.WriteLine("Use an independent PostgreSQL database and a dedicated loopback API. Actual API restart is performed by the CI owner between play and recover.");
    return;
}

var cases = new[] { "payment-resource-surplus", "damage-doubling-combat-keywords", "ven-flow-combat-keywords",
    "optional-additional-cost-reductions", "instruction-power-payment", "restricted-mana-lux", "empowered-target-action-replacement" };
var phases = new[] { "sql", "prepare", "play", "recover", "finish", "verify", "tamper" };
if (args.Length < 3 || !cases.Contains(args[0]) || !phases.Contains(args[1]))
    throw new ArgumentException("Specify one supported case, phase and output directory.");
var caseId = args[0]; var phase = args[1];
var hasFinish = caseId is "instruction-power-payment" or "restricted-mana-lux";
if (phase == "finish" && !hasFinish)
    throw new ArgumentException("Only instruction payment and restricted mana have a finish phase.");
if (phase == "verify" ? args.Length != 4 || args[3] is not ("before-restart" or "after-restart" or "final")
        || args[3] == "final" && !hasFinish : args.Length != 3)
    throw new ArgumentException("Only verify accepts a required before-restart, after-restart or applicable final stage.");
var output = Path.GetFullPath(args[2]);
Directory.CreateDirectory(output);
var stage = phase == "verify" ? args[3] : phase;
var sqlOutput = Path.Combine(output, "sql-cases");
var assemblies = new[] { typeof(CoreRuleEngine).Assembly, typeof(OfficialCardCatalog).Assembly,
    typeof(PlayerIntent).Assembly, typeof(PostgresMatchJournal).Assembly, typeof(Program).Assembly }.Distinct();
var runtime = new List<object>();
foreach (var assembly in assemblies)
    runtime.Add(new { file = Path.GetFileName(assembly.Location), path = assembly.Location,
        mvid = assembly.ManifestModule.ModuleVersionId,
        informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
        sha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(assembly.Location))).ToLowerInvariant() });
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
await using (var record = new FileStream(Path.Combine(output, "driver-runtime-" + stage + ".json"), FileMode.CreateNew))
    await JsonSerializer.SerializeAsync(record, new { caseId, phase, stage, processId = Environment.ProcessId,
        baseDirectory = AppContext.BaseDirectory, ruleInputs = RuleInputIdentity.Current, assemblies = runtime }, json);

if (phase is "play" or "recover" or "finish")
{
    var url = Environment.GetEnvironmentVariable("RIFTBOUND_SERVER_URL")
        ?? throw new ArgumentException("RIFTBOUND_SERVER_URL is required.");
    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !uri.IsLoopback || uri.Scheme != Uri.UriSchemeHttp)
        throw new ArgumentException("Only a dedicated loopback HTTP acceptance API is allowed.");
    switch (caseId)
    {
        case "payment-resource-surplus": await PaymentResourceSurplusNetworkAcceptance.RunAsync(url.TrimEnd('/'), output, phase == "recover"); break;
        case "damage-doubling-combat-keywords": await DamageDoublingCombatKeywordNetworkAcceptance.RunAsync(url.TrimEnd('/'), output, phase == "recover"); break;
        case "ven-flow-combat-keywords": await VenFlowCombatKeywordNetworkAcceptance.RunAsync(url.TrimEnd('/'), output, phase == "recover"); break;
        case "optional-additional-cost-reductions": await OptionalAdditionalCostReductionNetworkAcceptance.RunAsync(url.TrimEnd('/'), output, phase == "recover"); break;
        case "instruction-power-payment": await InstructionPowerPaymentNetworkAcceptance.RunAsync(url.TrimEnd('/'), output, phase); break;
        case "restricted-mana-lux": await RestrictedManaNetworkAcceptance.RunAsync(url.TrimEnd('/'), output, phase); break;
        case "empowered-target-action-replacement": await EmpoweredTargetReplacementNetworkAcceptance.RunAsync(url.TrimEnd('/'), output, phase == "recover"); break;
    }
}
else
{
    var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Riftbound");
    if (string.IsNullOrWhiteSpace(connection)) throw new ArgumentException("An independent ConnectionStrings__Riftbound is required.");
    var settings = new NpgsqlConnectionStringBuilder(connection);
    if (settings.Database != "riftbound_acceptance" || settings.Host is not ("127.0.0.1" or "localhost"))
        throw new ArgumentException("Use the dedicated loopback riftbound_acceptance database.");
    await using var source = NpgsqlDataSource.Create(connection);
    if (phase is "sql" or "prepare")
    {
        await using var database = await source.OpenConnectionAsync();
        foreach (var path in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Sql"), "*.sql").Order(StringComparer.Ordinal))
        {
            await using var command = new NpgsqlCommand(await File.ReadAllTextAsync(path), database);
            await command.ExecuteNonQueryAsync();
        }
    }
    switch (caseId, phase)
    {
        case ("payment-resource-surplus", "sql"): await PaymentResourceSurplusSqlAcceptance.RunAsync(source, sqlOutput); break;
        case ("payment-resource-surplus", "prepare"): await PaymentResourceSurplusSqlAcceptance.PrepareNetworkAsync(source,
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "card-play-history-current-opening-20261017.json"), output); break;
        case ("payment-resource-surplus", "verify"): await PaymentResourceSurplusSqlAcceptance.VerifyNetworkAsync(source, output, stage); break;
        case ("payment-resource-surplus", "tamper"): await PaymentResourceSurplusSqlAcceptance.VerifyNetworkTamperAsync(source, output); break;
        case ("damage-doubling-combat-keywords", "sql"): await DamageDoublingCombatKeywordSqlAcceptance.RunAsync(source, sqlOutput); break;
        case ("damage-doubling-combat-keywords", "prepare"): await DamageDoublingCombatKeywordSqlAcceptance.PrepareNetworkAsync(source, output); break;
        case ("damage-doubling-combat-keywords", "verify"): await DamageDoublingCombatKeywordSqlAcceptance.VerifyNetworkAsync(source, output, stage); break;
        case ("damage-doubling-combat-keywords", "tamper"): await DamageDoublingCombatKeywordSqlAcceptance.VerifyNetworkTamperAsync(source, output); break;
        case ("ven-flow-combat-keywords", "sql"): await VenFlowCombatKeywordSqlAcceptance.RunAsync(source, sqlOutput); break;
        case ("ven-flow-combat-keywords", "prepare"): await VenFlowCombatKeywordSqlAcceptance.PrepareNetworkAsync(source, output); break;
        case ("ven-flow-combat-keywords", "verify"): await VenFlowCombatKeywordSqlAcceptance.VerifyNetworkAsync(source, output, stage); break;
        case ("ven-flow-combat-keywords", "tamper"): await VenFlowCombatKeywordSqlAcceptance.VerifyNetworkTamperAsync(source, output); break;
        case ("optional-additional-cost-reductions", "sql"): await OptionalAdditionalCostReductionSqlAcceptance.RunAsync(source, sqlOutput); break;
        case ("optional-additional-cost-reductions", "prepare"): await OptionalAdditionalCostReductionSqlAcceptance.PrepareNetworkAsync(source, output); break;
        case ("optional-additional-cost-reductions", "verify"): await OptionalAdditionalCostReductionSqlAcceptance.VerifyNetworkAsync(source, output, stage); break;
        case ("optional-additional-cost-reductions", "tamper"): await OptionalAdditionalCostReductionSqlAcceptance.VerifyNetworkTamperAsync(source, output); break;
        case ("instruction-power-payment", "sql"): await SelectedBattlefieldDamageSqlAcceptance.RunAsync(source, sqlOutput); break;
        case ("instruction-power-payment", "prepare"): await InstructionPowerPaymentSqlAcceptance.PrepareAsync(source, output); break;
        case ("instruction-power-payment", "verify"): await InstructionPowerPaymentSqlAcceptance.VerifyAsync(source, output, stage); break;
        case ("instruction-power-payment", "tamper"): await InstructionPowerPaymentSqlAcceptance.TamperAsync(source, output); break;
        case ("restricted-mana-lux", "sql"): await RestrictedManaSqlAcceptance.RunAsync(source, sqlOutput); break;
        case ("restricted-mana-lux", "prepare"): await RestrictedManaSqlAcceptance.PrepareAsync(source, output); break;
        case ("restricted-mana-lux", "verify"): await RestrictedManaSqlAcceptance.VerifyAsync(source, output, stage); break;
        case ("restricted-mana-lux", "tamper"): await RestrictedManaSqlAcceptance.TamperAsync(source, output); break;
        case ("empowered-target-action-replacement", "sql"): await EmpoweredTargetReplacementSqlAcceptance.RunAsync(source, sqlOutput); break;
        case ("empowered-target-action-replacement", "prepare"): await EmpoweredTargetReplacementSqlAcceptance.PrepareNetworkAsync(source, output); break;
        case ("empowered-target-action-replacement", "verify"): await EmpoweredTargetReplacementSqlAcceptance.VerifyNetworkAsync(source, output, stage); break;
        case ("empowered-target-action-replacement", "tamper"): await EmpoweredTargetReplacementSqlAcceptance.VerifyNetworkTamperAsync(source, output); break;
        default: throw new ArgumentException("Unsupported acceptance stage.");
    }
}
await using (var receipt = new FileStream(Path.Combine(output, "driver-stage-" + stage + ".json"), FileMode.CreateNew))
    await JsonSerializer.SerializeAsync(receipt, new { status = "RELATED_BACKEND_STAGE_PASS", caseId, phase, stage,
        executedAtUtc = DateTimeOffset.UtcNow, fixedCommit = Environment.GetEnvironmentVariable("GITHUB_SHA"),
        wholeCardOrVisibleClientCertification = false }, json);
Console.WriteLine("RELATED_BACKEND_" + caseId + "_" + stage + "_PASS");
