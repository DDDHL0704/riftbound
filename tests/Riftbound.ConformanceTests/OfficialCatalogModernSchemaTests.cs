using Riftbound.CardCatalog;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OfficialCatalogModernSchemaTests
{
    [Fact]
    public async Task CurrentOfficialCatalogPreservesAllCategoriesWithoutSilentlyDroppingMultiTypeCards()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Riftbound.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var path = Path.Combine(root.FullName, "data", "official", "upstream", "2026-10-04", "card-catalog.zh-CN.json");
        var catalog = await OfficialCardCatalogLoader.LoadFromFileAsync(path);
        Assert.Equal(1374, catalog.Total);
        Assert.Equal(catalog.Total, catalog.Cards.Count);
        Assert.Equal(catalog.Total, catalog.Cards.Select(card => card.CardNo).Distinct().Count());
        Assert.All(catalog.Cards, card =>
        {
            Assert.NotEmpty(card.CardCategoryList);
            Assert.DoesNotContain("", card.CardCategoryList);
            Assert.Equal(card.CardCategoryList.Count, card.CardCategoryNameList.Count);
        });
        Assert.Contains(catalog.Cards, card => card.CardCategoryList.Count > 1);
    }
}
