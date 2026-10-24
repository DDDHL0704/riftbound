using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

// Compile this file alone as a library and opt in through DOTNET_STARTUP_HOOKS.
// It records the assembly actually loaded by the dedicated acceptance API.
internal static class StartupHook
{
    public static void Initialize()
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_PAYMENT_RUNTIME_RECEIPT") is not { Length: > 0 } path) return;
        AppDomain.CurrentDomain.AssemblyLoad += (_, args) => Capture(args.LoadedAssembly);
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) Capture(assembly);

        void Capture(Assembly assembly)
        {
            if (assembly.GetName().Name != "Riftbound.Engine") return;
            File.WriteAllText(path, JsonSerializer.Serialize(new {
                processId = Environment.ProcessId, capturedAtUtc = DateTimeOffset.UtcNow,
                baseDirectory = AppContext.BaseDirectory, loadedAssemblyPath = assembly.Location,
                sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))).ToLowerInvariant(),
                mvid = assembly.ManifestModule.ModuleVersionId
            }));
        }
    }
}
