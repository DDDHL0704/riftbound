using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;
using Xunit.Sdk;

// Enumerate data, never invoke a test body. VSTest cannot pre-enumerate some
// non-serializable MemberData; use the same pinned xUnit v2 display formatter.
var assemblyPath = Path.GetFullPath(args[0]);
var directory = Path.GetDirectoryName(assemblyPath)!;
AssemblyLoadContext.Default.Resolving += (_, name) =>
    File.Exists(Path.Combine(directory, name.Name + ".dll"))
        ? AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(directory, name.Name + ".dll")) : null;
var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
var requested = args[1] == "--modules-only" ? [] : JsonSerializer.Deserialize<string[]>(File.ReadAllText(args[1]))!;
var cases = new SortedDictionary<string, string[]>();
foreach (var fullName in requested)
{
    var split = fullName.LastIndexOf('.');
    var type = assembly.GetType(fullName[..split], throwOnError: true)!;
    var method = type.GetMethod(fullName[(split + 1)..], BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
        ?? throw new InvalidOperationException("Missing discovered method " + fullName);
    var fact = method.GetCustomAttributes<FactAttribute>(true).Single();
    if (fact.Skip is not null || fact.DisplayName is not null || method.IsGenericMethodDefinition)
        throw new InvalidOperationException("Unsupported or skipped case inventory: " + fullName);
    if (fact is not TheoryAttribute) { cases.Add(fullName, [fullName]); continue; }
    var rows = method.GetCustomAttributes<DataAttribute>(true)
        .SelectMany(attribute => attribute.GetData(method) ?? throw new InvalidOperationException("Null theory data"))
        .Select(row => TypeUtility.GetDisplayNameWithArguments(new ReflectionMethodInfo(method), fullName,
            TypeUtility.ResolveMethodArguments(method, row), null)).ToArray();
    if (rows.Length == 0) throw new InvalidOperationException("Empty theory data: " + fullName);
    cases.Add(fullName, rows);
}
var modules = new[] { "Riftbound.ConformanceTests", "Riftbound.Engine", "Riftbound.Contracts", "Riftbound.CardCatalog", "Riftbound.Persistence", "Riftbound.Api" }
    .Select(name => {
        var path = Path.Combine(directory, name + ".dll");
        var loaded = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        return new { name, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(),
            mvid = loaded.ManifestModule.ModuleVersionId,
            informationalVersion = loaded.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion };
    }).ToArray();
File.WriteAllText(args[2], JsonSerializer.Serialize(new { cases, modules }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
