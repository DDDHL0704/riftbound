using Riftbound.CardCatalog;
using Riftbound.Engine;
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

    [Fact]
    public async Task UncoveredPlayableFunctionalUnitsAreKnownComplexP2ScopeBlocks()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var uncoveredUnits = FunctionalUnitBuilder.Build(catalog.Cards)
            .Where(unit => !unit.Cards.Any(card => CardBehaviorRegistry.TryGetByCardNo(card.CardNo, out _)))
            .ToArray();

        var uncoveredPlayableUnits = uncoveredUnits
            .Where(unit => string.Equals(unit.Category, "法术", StringComparison.Ordinal)
                || string.Equals(unit.Category, "单位", StringComparison.Ordinal)
                || string.Equals(unit.Category, "装备", StringComparison.Ordinal))
            .Select(unit => $"{unit.RepresentativeNo} {unit.Name} {unit.Category}")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "OGN·025/298 暴怒冲动 法术",
                "OGN·080/298 倒转神通 法术",
                "OGN·115/298 光明未来 法术",
                "OGN·244/298 圣裁之刻 法术",
                "SFD·011/221 取放自如 法术"
            ],
            uncoveredPlayableUnits);

        var uncoveredNonPlayableCategories = uncoveredUnits
            .Where(unit => !uncoveredPlayableUnits.Any(value => value.StartsWith(unit.RepresentativeNo, StringComparison.Ordinal)))
            .GroupBy(unit => unit.Category, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        Assert.Equal(6, uncoveredNonPlayableCategories["符文"]);
        Assert.Equal(44, uncoveredNonPlayableCategories["传奇"]);
        Assert.Equal(54, uncoveredNonPlayableCategories["战场"]);
        Assert.Equal(2, uncoveredNonPlayableCategories["指示物战场"]);
        Assert.Equal(9, uncoveredNonPlayableCategories["指示物单位"]);
        Assert.Equal(2, uncoveredNonPlayableCategories["指示物装备"]);
    }
}
