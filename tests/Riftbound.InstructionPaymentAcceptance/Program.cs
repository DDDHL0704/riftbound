using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Npgsql;
using Riftbound.Engine;

if (args.Length == 0 || args[0] is "--help" or "help")
{
    Console.WriteLine("Usage: dotnet run --project tests/Riftbound.InstructionPaymentAcceptance -- <identity|sql|prepare|play|recover|finish|verify|tamper> <output> [before-restart|after-restart|final]");
    Console.WriteLine("Independent database: ConnectionStrings__Riftbound. Dedicated loopback HTTP API: RIFTBOUND_SERVER_URL. Flow: sql, prepare, start API, play, verify before-restart, actual stop/restart API, recover, verify after-restart, finish, verify final, stop API, tamper. sql retains the existing 17 selected-field cases; network uses 2 new logged QA cases, not ordinary openings.");
    return;
}
if (args.Length < 2 || args[0] is not ("identity" or "sql" or "prepare" or "play" or "recover" or "finish" or "verify" or "tamper"))
    throw new ArgumentException("Specify a supported selector and dedicated output; --help describes the complete flow.");
var selector = args[0];
if (selector == "verify" && (args.Length != 3 || args[2] is not ("before-restart" or "after-restart" or "final")))
    throw new ArgumentException("verify needs before-restart, after-restart or final.");
var output = Path.GetFullPath(args[1]); Directory.CreateDirectory(output);
var assembly = typeof(CoreRuleEngine).Assembly;
await File.WriteAllTextAsync(Path.Combine(output, "runtime-" + selector + (selector == "verify" ? "-" + args[2] : "") + ".json"),
    JsonSerializer.Serialize(new { processId = Environment.ProcessId, assemblyPath = assembly.Location,
        mvid = assembly.ManifestModule.ModuleVersionId,
        informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
        sha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(assembly.Location))).ToLowerInvariant() },
        new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
if (selector == "identity") return;
if (selector is "play" or "recover" or "finish")
{
    var url = Environment.GetEnvironmentVariable("RIFTBOUND_SERVER_URL")
        ?? throw new ArgumentException("RIFTBOUND_SERVER_URL must identify the dedicated acceptance API.");
    var uri = new Uri(url);
    if (!uri.IsLoopback || uri.Scheme != Uri.UriSchemeHttp) throw new ArgumentException("Use a dedicated loopback HTTP API.");
    await InstructionPowerPaymentNetworkAcceptance.RunAsync(url.TrimEnd('/'), output, selector);
}
else
{
    await using var source = NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("ConnectionStrings__Riftbound")
        ?? throw new ArgumentException("ConnectionStrings__Riftbound must identify an independent acceptance database."));
    if (selector is "sql" or "prepare")
    {
        await using var connection = await source.OpenConnectionAsync();
        foreach (var path in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Sql"), "*.sql").Order(StringComparer.Ordinal))
        {
            await using var command = new NpgsqlCommand(await File.ReadAllTextAsync(path), connection);
            await command.ExecuteNonQueryAsync();
        }
    }
    switch (selector)
    {
        case "sql": await SelectedBattlefieldDamageSqlAcceptance.RunAsync(source, Path.Combine(output, "sql-cases")); break;
        case "prepare": await InstructionPowerPaymentSqlAcceptance.PrepareAsync(source, output); break;
        case "verify": await InstructionPowerPaymentSqlAcceptance.VerifyAsync(source, output, args[2]); break;
        case "tamper": await InstructionPowerPaymentSqlAcceptance.TamperAsync(source, output); break;
    }
}
Console.WriteLine("INSTRUCTION_PAYMENT_" + selector.ToUpperInvariant() + "_PASS");
