using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Npgsql;
using Riftbound.CardCatalog;
using Riftbound.Engine;

if (args.Length == 0 || args[0] is "--help" or "help")
{
    Console.WriteLine("Usage: dotnet run --project tests/Riftbound.RestrictedManaAcceptance -- <identity|sql|prepare|play|recover|finish|verify|tamper> <output> [before-restart|after-restart|final]");
    Console.WriteLine("Independent database: ConnectionStrings__Riftbound; dedicated loopback HTTP API: RIFTBOUND_SERVER_URL. Remote flow: sql, prepare, start API, play, verify before-restart, actually stop/restart API, recover, verify after-restart, finish, verify final, stop API, tamper. Two exact Lux prints; original empty-room/seats/finite-QA birth, not ordinary-opening certification. Never runs these heavy gates implicitly.");
    return;
}
if (args.Length < 2 || args[0] is not ("identity" or "sql" or "prepare" or "play" or "recover" or "finish" or "verify" or "tamper"))
    throw new ArgumentException("Specify a supported selector and dedicated output; see --help.");
var selector = args[0];
if (selector == "verify" && (args.Length != 3 || args[2] is not ("before-restart" or "after-restart" or "final")))
    throw new ArgumentException("verify requires before-restart, after-restart or final.");
var output = Path.GetFullPath(args[1]); Directory.CreateDirectory(output);
var assembly = typeof(CoreRuleEngine).Assembly;
await File.WriteAllTextAsync(Path.Combine(output, "runtime-" + selector + (selector == "verify" ? "-" + args[2] : "") + ".json"),
    JsonSerializer.Serialize(new { processId = Environment.ProcessId, assemblyPath = assembly.Location,
        mvid = assembly.ManifestModule.ModuleVersionId,
        informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
        sha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(assembly.Location))).ToLowerInvariant(),
        catalogPath = OfficialCardCatalog.DefaultCatalogPath(), catalogSize = (await OfficialCardCatalog.LoadDefaultAsync()).Cards.Count,
        ruleInputs = RuleInputIdentity.Current }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
if (selector == "identity") return;
if (selector is "play" or "recover" or "finish")
{
    var url = Environment.GetEnvironmentVariable("RIFTBOUND_SERVER_URL") ?? throw new ArgumentException("Missing dedicated API URL.");
    var uri = new Uri(url);
    if (!uri.IsLoopback || uri.Scheme != Uri.UriSchemeHttp) throw new ArgumentException("Use a dedicated loopback HTTP API.");
    await RestrictedManaNetworkAcceptance.RunAsync(url.TrimEnd('/'), output, selector);
}
else
{
    await using var source = NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("ConnectionStrings__Riftbound")
        ?? throw new ArgumentException("Missing independent acceptance database connection string."));
    if (selector is "sql" or "prepare")
    {
        await using var connection = await source.OpenConnectionAsync();
        foreach (var path in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Sql"), "*.sql").Order(StringComparer.Ordinal))
        { await using var command = new NpgsqlCommand(await File.ReadAllTextAsync(path), connection); await command.ExecuteNonQueryAsync(); }
    }
    switch (selector)
    {
        case "sql": await RestrictedManaSqlAcceptance.RunAsync(source, output); break;
        case "prepare": await RestrictedManaSqlAcceptance.PrepareAsync(source, output); break;
        case "verify": await RestrictedManaSqlAcceptance.VerifyAsync(source, output, args[2]); break;
        case "tamper": await RestrictedManaSqlAcceptance.TamperAsync(source, output); break;
    }
}
Console.WriteLine("RESTRICTED_MANA_" + selector.ToUpperInvariant() + "_PASS");
