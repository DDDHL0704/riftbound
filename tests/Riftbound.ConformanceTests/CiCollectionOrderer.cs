using System.Text.Json;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

[assembly: TestCollectionOrderer("Riftbound.ConformanceTests.CiCollectionOrderer", "Riftbound.ConformanceTests")]

namespace Riftbound.ConformanceTests;

// Start long classes first so a final slow collection cannot leave the other
// workers idle. This only reorders the collections xUnit has already selected.
public sealed class CiCollectionOrderer : ITestCollectionOrderer
{
    private readonly string? selectionPath;

    public CiCollectionOrderer() : this(Environment.GetEnvironmentVariable("RIFTBOUND_TEST_COLLECTION_ORDER")) { }
    internal CiCollectionOrderer(string? selectionPath) => this.selectionPath = selectionPath;

    public IEnumerable<ITestCollection> OrderTestCollections(IEnumerable<ITestCollection> testCollections)
    {
        var defaults = new DefaultTestCollectionOrderer().OrderTestCollections(testCollections);
        var estimates = ReadEstimates();
        if (estimates is null) return defaults;
        const string prefix = "Test collection for ";
        return defaults.OrderByDescending(collection => collection.DisplayName.StartsWith(prefix, StringComparison.Ordinal)
            ? estimates.GetValueOrDefault(collection.DisplayName[prefix.Length..]) : 0);
    }

    private Dictionary<string, double>? ReadEstimates()
    {
        if (string.IsNullOrEmpty(selectionPath)) return null;
        try
        {
            using var selection = JsonDocument.Parse(File.ReadAllText(selectionPath));
            if (selection.RootElement.ValueKind != JsonValueKind.Object
                || !selection.RootElement.TryGetProperty("estimatedClassSeconds", out var values)
                || values.ValueKind != JsonValueKind.Object) return null;
            var estimates = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var value in values.EnumerateObject())
                if (value.Value.ValueKind != JsonValueKind.Number || !value.Value.TryGetDouble(out var seconds)
                    || !double.IsFinite(seconds) || seconds < 0 || !estimates.TryAdd(value.Name, seconds)) return null;
            return estimates;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            // Timing estimates are optional; unavailable inputs keep xUnit's
            // usual order and never remove tests or change execution results.
            return null;
        }
    }
}
