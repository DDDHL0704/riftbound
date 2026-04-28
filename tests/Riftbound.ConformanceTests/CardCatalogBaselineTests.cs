using Riftbound.CardCatalog;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class CardCatalogBaselineTests
{
    [Fact]
    public async Task OfficialCatalogLoadsAllSnapshotCards()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);

        Assert.Equal("https://playloltcg.com/card.html", catalog.Source);
        Assert.Equal("2026-04-27", catalog.FetchedAt);
        Assert.Equal(1009, catalog.Total);
        Assert.Equal(1009, catalog.Cards.Count);
    }

    [Fact]
    public async Task FunctionalUnitsMatchCurrentBaselineCounts()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var summary = FunctionalUnitBuilder.Summarize(units);

        Assert.Equal(1009, summary.OfficialEntries);
        Assert.Equal(811, summary.FunctionalUnits);
        Assert.Equal(113, summary.DuplicateGroups);
        Assert.Equal(311, summary.DuplicateEntries);
        Assert.Equal(198, summary.SavedLogicImplementations);
    }
}
