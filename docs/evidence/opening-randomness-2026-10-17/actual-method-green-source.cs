using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

const string repo = "/Users/dinghaolin/MyProjects/riftbound";
var output = Path.Combine(repo, "var/qa/opening-shuffle-green-probe-20261017");
var native = Path.Combine(repo, "var/qa/created-token-ready-paired-20261017");
var publish = Path.Combine(repo, "tests/Riftbound.PostgresAcceptance/bin/Debug/net10.0");
var resultPath = Path.Combine(output, "actual-method-probe.json");
if (File.Exists(resultPath)) throw new Exception("Never overwrite a prior probe");
static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
using var inputs = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, "var/qa/opening-shuffle-probe-20261017/sql-v3/runtime-inputs-before.json")));
var enginePath = Path.Combine(publish, "Riftbound.Engine.dll");
if (Hash(enginePath) != inputs.RootElement.GetProperty("Riftbound.Engine.dll").GetString())
    throw new Exception("Current frozen SQL-v3 engine input changed");
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    var path = Path.Combine(publish, name.Name + ".dll");
    return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
};
var engine = AssemblyLoadContext.Default.LoadFromAssemblyPath(enginePath);
var type = engine.GetType("Riftbound.Engine.MatchSession", throwOnError: true)!;
var method = type.GetMethod("RandomizeStable", BindingFlags.NonPublic | BindingFlags.Static)!
    .MakeGenericMethod(typeof(string));

// Read only the public submitted deck blueprint. Never read any match seed or future deck order.
var deckPath = Path.Combine(native, "custom-deck-a.json");
using var deck = JsonDocument.Parse(File.ReadAllText(deckPath));
var cards = deck.RootElement.GetProperty("mainDeck").EnumerateArray().Select(x => x.GetString()!).ToList();
var champion = deck.RootElement.GetProperty("championCardNo").GetString()!;
cards.RemoveAt(cards.FindIndex(x => x == champion));
if (cards.Count != 39 || cards.Count(x => x == "SFD·198/221") != 3) throw new Exception("Expected public39-card library with3 exact physical Rise copies");

const int samples = 20000;
const int probeSeed = 20261017;
var random = new Random(probeSeed);
var seeds = Enumerable.Range(0, samples).Select(_ => random.NextInt64(long.MaxValue)).ToArray();
var seedPath = Path.Combine(output, "independent-synthetic-seeds.txt");
File.WriteAllLines(seedPath, seeds.Select(x => x.ToString(System.Globalization.CultureInfo.InvariantCulture)));
var positionsPath = Path.Combine(output, "observed-copy-positions.tsv");
using var positions = new StreamWriter(positionsPath);
positions.WriteLine("role\tsample\tposition1\tposition2\tposition3");
var rows = new List<object>();
var watch = Stopwatch.StartNew();
foreach (var (player, cursor) in new[] { ("shuffle-probe-a", 1L), ("shuffle-probe-b", 4L) })
{
    var values = cards.Select((card, index) => player + "-MAIN-" + new string(card.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-') + "-" + (index + 1).ToString("00")).ToArray();
    var copies = values.Where((_, index) => cards[index] == "SFD·198/221").ToHashSet();
    var allOpening = 0;
    var allTop10 = 0;
    var contiguous = 0;
    var openingCopySum = 0;
    for (var sample = 0; sample < seeds.Length; sample++)
    {
        var result = (IReadOnlyList<string>)method.Invoke(null, [values, seeds[sample], cursor, player + ":MAIN_DECK"])!;
        if (result.Count != 39 || result.Distinct().Count() != 39 || !result.ToHashSet().SetEquals(values))
            throw new Exception("Actual production method did not return the exact input permutation");
        var found = result.Select((value, index) => (value, position: index + 1)).Where(x => copies.Contains(x.value)).Select(x => x.position).ToArray();
        if (found.Length != 3) throw new Exception("Physical copies missing");
        if (found[^1] <= 4) allOpening++;
        if (found[^1] <= 10) allTop10++;
        if (found[^1] - found[0] == 2) contiguous++;
        openingCopySum += found.Count(x => x <= 4);
        positions.WriteLine($"{player}\t{sample}\t{found[0]}\t{found[1]}\t{found[2]}");
    }
    rows.Add(new
    {
        syntheticPlayerId = player, rngCursor = cursor, samples,
        allThreeInOpeningFour = allOpening, allThreeInOpeningFourRate = (double)allOpening / samples,
        allThreeInTopTen = allTop10, allThreeInTopTenRate = (double)allTop10 / samples,
        allThreeContiguous = contiguous, allThreeContiguousRate = (double)contiguous / samples,
        meanCopiesInOpeningFour = (double)openingCopySum / samples,
    });
}
positions.Flush();
watch.Stop();
var record = new
{
    status = "ACTUAL_CURRENT_UNCOMMITTED_OPENING_METHOD_DISTRIBUTION_MEASURED_NOT_WHOLE_FAMILY_CERTIFIED",
    verifiedAtUtc = DateTimeOffset.UtcNow,
    baselineSourceCommit = "a6ccf1952141c69ed4b3cde94695f983c1c28db8", currentImplementationIncludesUncommittedChanges = true,
    actualLoadedEngineSha256 = Hash(enginePath), actualLoadedEngineMvid = engine.ManifestModule.ModuleVersionId,
    method = "Riftbound.Engine.MatchSession.RandomizeStable<string>",
    implementationInvokedThroughReflectionNoReimplementation = true,
    publicSubmittedDeckSha256 = Hash(deckPath), deckCount = 39, distinctPhysicalCopiesObserved = 3,
    probeSeed, samplesPerRole = samples, totalActualProductionInvocations = samples * 2,
    uniformPermutationCombinatorialReference = new
    {
        denominatorChoose39By3 = 9139,
        allThreeInOpeningFourNumeratorChoose4By3 = 4, allThreeInOpeningFourProbability = 4d / 9139,
        allThreeInTopTenNumeratorChoose10By3 = 120, allThreeInTopTenProbability = 120d / 9139,
        allThreeContiguousNumerator = 37, allThreeContiguousProbability = 37d / 9139,
        expectedMeanCopiesInOpeningFour = 4d * 3 / 39,
        referenceIndependentOfProductionHashImplementation = true,
    },
    results = rows, elapsedSeconds = watch.Elapsed.TotalSeconds,
    syntheticSeedFile = new { path = seedPath, sha256 = Hash(seedPath) },
    observedPositionsFile = new { path = positionsPath, sha256 = Hash(positionsPath) },
    probeSource = new { path = Path.Combine(output, "Program.cs"), sha256 = Hash(Path.Combine(output, "Program.cs")) },
    noActualMatchSeedOrFutureDeckOrderRead = true,
    original143FullGames110PairsAndSeed424242NotModified = true,
    originalMistakenNativeCardImageObservationNotUsedAsEvidence = true,
    productionFilesAndRunningMatchesNotModified = true,
    officialPregameAndShuffleClauseReviewAndImplementationDisposition = "LIMITED_FIX_MEASURED; final check/fixed CI/product/other randomness remain OPEN",
    wholeCardOrGlobalCertification = false,
};
File.WriteAllText(resultPath, JsonSerializer.Serialize(record, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }) + "\n");
Console.WriteLine(JsonSerializer.Serialize(new { record.status, record.totalActualProductionInvocations, results = rows, record.elapsedSeconds }));
