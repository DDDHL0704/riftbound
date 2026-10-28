using Riftbound.CardCatalog;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ScopedOfficialCardSourcesTests
{
    [Theory]
    [InlineData("RAD·054/167", 40126, "饰品商贩", 3, 3, "约德尔人")]
    [InlineData("RAD·055/167", 40127, "叮响巡警", 3, 2, "机械")]
    [InlineData("RAD·050/167", 40122, "神秘的时间法师", 5, 4, "")]
    public void ExactScopedPrintUsesTheCompleteOfficialCatalogNormalizer(
        string cardNo, long id, string name, int mana, int might, string tag)
    {
        Assert.True(ScopedOfficialCardSources.TryGet(cardNo, out var card));
        Assert.Equal(id, card.Id);
        Assert.Equal(cardNo, card.CardNo);
        Assert.Equal(name, card.CardName);
        Assert.Equal(mana, card.Energy);
        Assert.Equal(might, card.Power);
        Assert.Null(card.ReturnEnergy);
        Assert.Equal("单位", card.CardCategoryName);
        Assert.Equal(["unit"], card.CardCategoryList);
        Assert.Equal(["单位"], card.CardCategoryNameList);
        Assert.Equal(["blue"], card.CardColorList);
        Assert.Equal(tag, card.Tag);
        Assert.False(string.IsNullOrWhiteSpace(card.FrontImage));
        Assert.Equal(card.CardNo, Assert.Single(ScopedOfficialCardSources.GetAll(), c => c.Id == id).CardNo);
    }

    [Fact]
    public void TheSharedPhysicalAndKeywordSourcesRetainTheSameFrozenIdentity()
    {
        Assert.Equal("6f60dcf82b29b3b0914bd8e26e3de8f3d30e05846f0a44050b4269604246a555",
            ScopedOfficialCardSources.SnapshotSha256);
        Assert.Equal(DisarmSourceSpecifications.SnapshotPath, ScopedOfficialCardSources.SnapshotPath);
        Assert.Equal(DisarmSourceSpecifications.SnapshotSha256, ScopedOfficialCardSources.SnapshotSha256);
        Assert.Equal(14, ScopedOfficialCardSources.GetAll().Count);
        foreach (var card in ScopedOfficialCardSources.GetAll())
        {
            Assert.True(DisarmSourceSpecifications.TryGet(card.CardNo, out var keyword));
            Assert.Equal(card.Id, keyword.SourceId);
            Assert.Equal(card.CardName, keyword.CardName);
            Assert.Equal(card.CardEffect, keyword.EffectiveText);
        }
    }

    [Fact]
    public void ACallerCannotRewriteTheSharedPrintOrItsCollectionFields()
    {
        var cards = ScopedOfficialCardSources.GetAll();
        var original = cards[0];
        var originalColors = original.CardColorList.ToArray();
        var list = Assert.IsAssignableFrom<IList<OfficialCard>>(cards);
        Assert.Throws<NotSupportedException>(() => list[0] = original with { CardName = "FORGED" });
        var colors = Assert.IsAssignableFrom<IList<string>>(original.CardColorList);
        Assert.Throws<NotSupportedException>(() => colors[0] = "red");
        Assert.Equal(original.CardName, ScopedOfficialCardSources.GetAll()[0].CardName);
        Assert.Equal(originalColors, ScopedOfficialCardSources.GetAll()[0].CardColorList);
    }

    [Fact]
    public void AScopedSnapshotDoesNotInferNamesOtherPrintsOrOtherCatalogEntries()
    {
        foreach (var cardNo in new string?[] { null, "", "RAD·054", "RAD·054a/167", "饰品商贩",
            " RAD·054/167", "RAD·054/167 ", "OGN·010/298" })
            Assert.False(ScopedOfficialCardSources.TryGet(cardNo, out _));
    }
}
