using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Npgsql;
using Riftbound.Engine;

if (args.Length == 0 || args[0] is "--help" or "help")
{
    Console.WriteLine("Usage: dotnet run --project tests/Riftbound.PaymentResourceSurplusAcceptance -- <sql|prepare|play|recover|verify|tamper> <output-directory> [before-restart|after-restart]");
    Console.WriteLine("Use one dedicated loopback riftbound_acceptance ConnectionStrings__Riftbound for SQL, API and all network phases; RIFTBOUND_SERVER_URL identifies the dedicated local HTTP API.");
    Console.WriteLine("Flow: sql, prepare, start API, play, verify before-restart, actually stop/restart API, recover, verify after-restart, stop API, tamper. Joined token sidecars stay in the same output directory.");
    return;
}
if (args.Length < 2 || args[0] is not ("sql" or "prepare" or "play" or "recover" or "verify" or "tamper"))
    throw new ArgumentException("Specify a supported selector and a dedicated output directory; --help lists the complete flow.");
var selector = args[0];
if (selector == "verify" ? args.Length != 3 || args[2] is not ("before-restart" or "after-restart") : args.Length != 2)
    throw new ArgumentException("verify requires exactly before-restart or after-restart; other selectors require only the output directory.");
var output = Path.GetFullPath(args[1]);
Directory.CreateDirectory(output);
var assembly = typeof(CoreRuleEngine).Assembly;
await File.WriteAllTextAsync(Path.Combine(output, "runtime-" + selector + ".json"), JsonSerializer.Serialize(new {
    processId = Environment.ProcessId, baseDirectory = AppContext.BaseDirectory, assemblyPath = assembly.Location,
    mvid = assembly.ManifestModule.ModuleVersionId,
    informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
    sha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(assembly.Location))).ToLowerInvariant()
}, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));

if (selector is "play" or "recover")
{
    var url = Environment.GetEnvironmentVariable("RIFTBOUND_SERVER_URL")
        ?? throw new ArgumentException("RIFTBOUND_SERVER_URL must identify the dedicated acceptance API.");
    var uri = new Uri(url);
    if (!uri.IsLoopback || uri.Scheme != Uri.UriSchemeHttp)
        throw new ArgumentException("The acceptance client requires a dedicated local HTTP API.");
    await PaymentResourceSurplusNetworkAcceptance.RunAsync(url.TrimEnd('/'), output, selector == "recover");
}
else
{
    await using var source = PaymentReconnectEvidence.AcceptanceSource(
        Environment.GetEnvironmentVariable("ConnectionStrings__Riftbound")
        ?? throw new ArgumentException("ConnectionStrings__Riftbound must identify the dedicated acceptance database."));
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
        case "sql": await PaymentResourceSurplusSqlAcceptance.RunAsync(source, output); break;
        case "prepare": await PaymentResourceSurplusSqlAcceptance.PrepareNetworkAsync(source,
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "card-play-history-current-opening-20261017.json"), output); break;
        case "verify": await PaymentResourceSurplusSqlAcceptance.VerifyNetworkAsync(source, output, args[2]); break;
        case "tamper": await PaymentResourceSurplusSqlAcceptance.VerifyNetworkTamperAsync(source, output); break;
    }
}
Console.WriteLine("PAYMENT_RESOURCE_SURPLUS_ACCEPTANCE_" + selector.ToUpperInvariant() + "_PASS");
