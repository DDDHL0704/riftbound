using Riftbound.CardCatalog;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class DisarmSourceSpecificationTests
{
    [Theory]
    [InlineData("RAD·054/167", 40126, 1, "", 0)]
    [InlineData("RAD·050/167", 40122, 2, "", 0)]
    [InlineData("RAD·055/167", 40127, 0, "机械", 1)]
    public void ExactPrintSeparatesPrintedValuesFromIndependentFriendlyGrants(
        string cardNo, long id, int printedValue, string grantTag, int grantValue)
    {
        Assert.True(DisarmSourceSpecifications.TryGet(cardNo, out var specification));
        Assert.Equal(cardNo, specification.CardNo);
        Assert.Equal(id, specification.SourceId);
        Assert.Equal(printedValue, specification.PrintedValue);
        Assert.Contains("{{缴械", specification.EffectiveText, StringComparison.Ordinal);
        if (grantTag.Length == 0) Assert.Empty(specification.FriendlyGrants);
        else
        {
            var grant = Assert.Single(specification.FriendlyGrants);
            Assert.Equal(grantTag, grant.RequiredUnitTag);
            Assert.Equal(grantValue, grant.Value);
            Assert.Contains(grantTag, specification.UnitTags);
        }
        if (id == 40122) Assert.Contains("{{洞察2}}", specification.EffectiveText, StringComparison.Ordinal);
    }

    [Fact]
    public void MetadataRetainsTheExactSnapshotAndDoesNotInferOtherPrintsOrNames()
    {
        Assert.Equal("9cd6f83fc4061b2f83f36ff4607d344d9f7caefa72280de055f642e1f80c7340",
            DisarmSourceSpecifications.SnapshotSha256);
        Assert.Equal(3, DisarmSourceSpecifications.GetAll().Count);
        foreach (var unknown in new[] { "RAD·054", "RAD·054a/167", "RAD·050a/167", "RAD·055a/167",
            "饰品商贩", "OGN·010/298", " RAD·054/167", "RAD·054/167 " })
            Assert.False(DisarmSourceSpecifications.TryGet(unknown, out _));
    }

    [Fact]
    public async Task SourceMetadataDoesNotEnableIncompleteCardsOrChangeTheRuntimeCatalog()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        Assert.Equal(1089, catalog.Total);
        Assert.Equal(1089, catalog.Cards.Count);
        Assert.Equal(870, FunctionalUnitBuilder.Summarize(FunctionalUnitBuilder.Build(catalog.Cards)).FunctionalUnits);
        foreach (var specification in DisarmSourceSpecifications.GetAll())
        {
            Assert.DoesNotContain(catalog.Cards, card => card.CardNo == specification.CardNo);
            Assert.False(CardBehaviorRegistry.TryGetByCardNo(specification.CardNo, out _));
        }
    }
}
