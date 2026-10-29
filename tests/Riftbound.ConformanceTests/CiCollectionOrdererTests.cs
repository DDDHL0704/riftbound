using System.Text.Json;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Riftbound.ConformanceTests;

public sealed class CiCollectionOrdererTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "riftbound-collection-order-" + Guid.NewGuid());

    [Fact]
    public void LongClassesStartFirstAndUnmeasuredCollectionsStillExecute()
    {
        ITestCollection[] collections = [Collection("Fast"), Collection("Unmeasured"), Collection("Slow")];
        var path = Write(JsonSerializer.Serialize(new { estimatedClassSeconds = new { Fast = 1, Slow = 900, Foreign = 10000 } }));
        var ordered = new CiCollectionOrderer(path).OrderTestCollections(collections).ToArray();
        Assert.Equal(new[] { collections[2], collections[0], collections[1] }, ordered);
        Assert.Equal(collections.Select(c => c.UniqueID).Order(), ordered.Select(c => c.UniqueID).Order());
    }

    [Fact]
    public void EqualWeightsRetainTheDefaultOrderAndNamedCollectionsRemainPresent()
    {
        ITestCollection[] collections = [Collection("Fast"), Collection("Slow"), NamedCollection("Named group")];
        var path = Write("{\"estimatedClassSeconds\":{\"Fast\":0,\"Slow\":0}}");
        Assert.Equal(new DefaultTestCollectionOrderer().OrderTestCollections(collections),
            new CiCollectionOrderer(path).OrderTestCollections(collections));
    }

    [Theory]
    [InlineData("unset", null)]
    [InlineData("truncated", "{")]
    [InlineData("array", "[]")]
    [InlineData("missing-weights", "{}")]
    [InlineData("array-weights", "{\"estimatedClassSeconds\":[]}")]
    [InlineData("negative", "{\"estimatedClassSeconds\":{\"Slow\":-1}}")]
    [InlineData("string", "{\"estimatedClassSeconds\":{\"Slow\":\"900\"}}")]
    [InlineData("infinite", "{\"estimatedClassSeconds\":{\"Slow\":1e400}}")]
    [InlineData("duplicate", "{\"estimatedClassSeconds\":{\"Slow\":1,\"Slow\":2}}")]
    public void MissingOrInvalidEstimatesKeepTheDefaultCompleteCollectionList(string scenario, string? json)
    {
        ITestCollection[] collections = [Collection("Fast"), Collection("Slow"), Collection("Unmeasured")];
        Assert.Equal(new DefaultTestCollectionOrderer().OrderTestCollections(collections),
            new CiCollectionOrderer(json is null ? null : Write(json, scenario)).OrderTestCollections(collections));
    }

    [Fact]
    public void AnUnavailableSelectionFileKeepsEveryCollection()
    {
        ITestCollection[] collections = [Collection("Slow"), Collection("Fast")];
        Assert.Equal(new DefaultTestCollectionOrderer().OrderTestCollections(collections),
            new CiCollectionOrderer(Path.Combine(directory, "missing.json")).OrderTestCollections(collections));
    }

    private string Write(string json, string name = "selection")
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name + ".json"); File.WriteAllText(path, json); return path;
    }
    private static ITestCollection Collection(string name) => NamedCollection("Test collection for " + name);
    private static ITestCollection NamedCollection(string name) =>
        new TestCollection(new TestAssembly(Reflector.Wrap(typeof(CiCollectionOrdererTests).Assembly)), null, name);
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
}
