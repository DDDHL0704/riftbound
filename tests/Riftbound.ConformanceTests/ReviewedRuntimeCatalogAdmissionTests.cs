using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ReviewedRuntimeCatalogAdmissionTests
{
    private const string PreviousCatalogIdentity = "e778e6e29472fbaccee0066f01a5f53c5f8191fd228a29f59073733b1a853682";
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task RuntimeSnapshotAddsTheImplementedReviewedPrintsAndKeepsTheOriginalImmutable()
    {
        var original = await OfficialCardCatalog.LoadDefaultAsync();
        var runtime = await OfficialCardCatalog.LoadRuntimeAsync();
        Assert.Same(runtime, await OfficialCardCatalog.LoadRuntimeAsync());
        Assert.Equal(1089, original.Total);
        Assert.Equal(1089, original.Cards.Count);
        Assert.Equal(1125, runtime.Total);
        Assert.Equal(1125, runtime.Cards.Count);
        Assert.Contains(ReviewedRuneCardSources.SnapshotPath, runtime.Source);
        Assert.Contains(VendettaCardSources.SnapshotPath, runtime.Source);
        Assert.Contains(ReviewedResourceLegendSources.SnapshotPath, runtime.Source);
        var originalNos = original.Cards.Select(card => card.CardNo).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(["VEN·005", "VEN·006", "VEN·009", "VEN·019", "VEN·019a", "VEN·020", "VEN·022", "VEN·024", "VEN·028", "VEN·038", "VEN·038a", "VEN·044", "VEN·067", "VEN·071", "VEN·079", "VEN·080", "VEN·091", "VEN·094", "VEN·102", "VEN·108", "VEN·121", "VEN·139", "VEN·141", "VEN·147", "VEN·151", "VEN·153", "VEN·189", "VEN·189*", "VEN·190", "VEN·190*", "VEN·193", "VEN·193*", "VEN·195", "VEN·195*", "VEN·196", "VEN·196*"], runtime.Cards.Where(card => !originalNos.Contains(card.CardNo))
