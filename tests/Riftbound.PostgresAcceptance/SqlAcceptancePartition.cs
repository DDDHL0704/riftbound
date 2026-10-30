using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal sealed class SqlAcceptancePartition
{
    private sealed record Case(string Id, string SourceType);
    private sealed record Manifest(int SchemaVersion, string Kind, int DefaultPartitionCount, IReadOnlyList<Case> Cases);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly string ManifestPath = Path.Combine(AppContext.BaseDirectory, "SqlAcceptanceCases.json");
    private readonly Manifest manifest;
    private readonly int? index;
    private readonly int count;
    private readonly List<object> executions = [];
    private readonly List<string> completed = [];
    private readonly HashSet<string> started = new(StringComparer.Ordinal);

    private SqlAcceptancePartition(Manifest manifest, int? index, int count)
    {
        this.manifest = manifest; this.index = index; this.count = count;
    }

    internal bool IsPartition => index is not null;
    private static Manifest ReadManifest()
    {
        var value = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(ManifestPath), Json)
            ?? throw new InvalidOperationException("SQL case identity manifest is required.");
        if (value.SchemaVersion != 1 || value.Kind != "SQL_EXECUTION_IDENTITIES_NOT_RULE_CERTIFICATION"
            || value.DefaultPartitionCount <= 0 || value.Cases.Count == 0
            || value.Cases.Any(c => string.IsNullOrWhiteSpace(c.Id) || string.IsNullOrWhiteSpace(c.SourceType))
            || value.Cases.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != value.Cases.Count
            || value.Cases.Count(c => c.Id == "core-opening" && c.SourceType == "PROGRAM_CORE_OPENING") != 1)
            throw new InvalidOperationException("Invalid or duplicated SQL execution identities.");
        return value;
    }

    internal static string PlanJson()
    {
        var value = ReadManifest();
        return JsonSerializer.Serialize(new { status = "PLAN_ONLY_NOT_DATABASE_ACCEPTANCE", value.SchemaVersion,
            value.Kind, value.DefaultPartitionCount, value.Cases,
            manifestSha256 = FileHash(ManifestPath) }, Json);
    }

    internal static SqlAcceptancePartition Parse(string[] args)
    {
        var value = ReadManifest();
        if (args.FirstOrDefault() != "--ci-partition") return new(value, null, 1);
        if (args.Length != 3 || !int.TryParse(args[1], out var index) || !int.TryParse(args[2], out var count)
            || count < 1 || count > value.Cases.Count || index < 0 || index >= count || index >= value.Cases.Count)
            throw new ArgumentException("Use --ci-partition INDEX COUNT with a nonempty valid partition.");
        return new(value, index, count);
    }

    private string[] Expected() => manifest.Cases.Where((_, i) => index is null || i % count == index)
        .Select(c => c.Id).ToArray();

    internal async Task RunAsync(string id, Func<Task> run)
    {
        // Even an unselected unknown call fails: a new helper cannot disappear
        // from the fixed execution identity list or from aggregate coverage.
        var ordinal = manifest.Cases.Select(c => c.Id).ToList().IndexOf(id);
        if (ordinal < 0 || !started.Add(id)) throw new InvalidOperationException("Unknown or duplicated default SQL case: " + id);
        if (index is not null && ordinal % count != index) return;
        var startedAt = DateTimeOffset.UtcNow; var watch = Stopwatch.StartNew();
        Console.WriteLine("SQL_CASE_STARTED " + id);
        await run();
        watch.Stop(); completed.Add(id);
        executions.Add(new { id, ordinal, status = "PASSED", startedAtUtc = startedAt,
            finishedAtUtc = DateTimeOffset.UtcNow, seconds = watch.Elapsed.TotalSeconds });
        Console.WriteLine("SQL_CASE_PASSED " + id);
    }

    internal async Task CompleteAsync(string output)
    {
        var expected = Expected();
        if (expected.Length == 0 || !completed.SequenceEqual(expected)
            || started.Count != manifest.Cases.Count)
            throw new InvalidOperationException("Every default SQL case must be present exactly once in its original order.");
        var file = Path.Combine(output, "partition-receipt.json");
        if (File.Exists(file)) throw new InvalidOperationException("Never replace a SQL partition receipt.");
        var assemblies = new[] { typeof(SqlAcceptancePartition).Assembly, typeof(CoreRuleEngine).Assembly,
            typeof(GameCommand).Assembly, typeof(OfficialCardCatalog).Assembly, typeof(PostgresMatchJournal).Assembly };
        var outputs = expected.SelectMany(id => id == "core-opening"
            ? new[] { Path.Combine(output, "result.json"), Path.Combine(output, "sql-counts.json") }.Where(File.Exists)
            : Directory.Exists(Path.Combine(output, id)) ? Directory.EnumerateFiles(Path.Combine(output, id), "*", SearchOption.AllDirectories) : [])
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (expected.Any(id => id == "core-opening" ? !File.Exists(Path.Combine(output, "result.json"))
                : !Directory.Exists(Path.Combine(output, id)) || !Directory.EnumerateFiles(Path.Combine(output, id), "*", SearchOption.AllDirectories).Any()))
            throw new InvalidOperationException("Every executed SQL case must retain its actual outputs.");
        var receipt = new {
            status = IsPartition ? "REAL_POSTGRES_PARTITION_COMPLETE" : "REAL_POSTGRES_ALL_DEFAULT_CASES_COMPLETE",
            fixedCommit = Environment.GetEnvironmentVariable("RIFTBOUND_FIXED_COMMIT") ?? Environment.GetEnvironmentVariable("GITHUB_SHA"), partitionIndex = index, partitionCount = count,
            manifestSha256 = FileHash(ManifestPath), orderedDefaultCases = manifest.Cases.Select(c => c.Id),
            expectedCases = expected, completedCases = completed, executions,
            executionRuntime = new { processorCount = Environment.ProcessorCount,
                serverGC = System.Runtime.GCSettings.IsServerGC,
                regexCacheSize = System.Text.RegularExpressions.Regex.CacheSize },
            actualRuleInputs = RuleInputIdentity.Current,
            outputFiles = outputs.Select(path => new { path = Path.GetRelativePath(output, path), bytes = new FileInfo(path).Length, sha256 = FileHash(path) }),
            runtimeAssemblies = assemblies.Select(a => new { file = Path.GetFileName(a.Location),
                sha256 = FileHash(a.Location), mvid = a.ManifestModule.ModuleVersionId }),
            nativeVisibleOrWholeRuleCertification = false };
        await using var stream = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, receipt, Json);
    }

    private static string FileHash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
