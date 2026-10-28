using Riftbound.CardCatalog;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ScopedFieldReviewAdmissionTests
{
    [Fact]
    public void ReviewedFieldsStayImmutableAndDoNotTurnResourceFactsIntoPhysicalAdmission()
    {
        var physical = ScopedOfficialCardSources.GetPhysicalPrints();
        var costs = ScopedOfficialCardSources.GetPrintedResourceCostPrints();
        Assert.Equal(13, physical.Count); Assert.Equal(14, costs.Count);
        Assert.DoesNotContain(physical, card => card.Id == 40299 || card.CardNo == "RAD·SP4/005");
        var disputed = Assert.Single(costs, card => card.Id == 40299);
        Assert.Equal("RAD·SP4/005", disputed.CardNo);
        Assert.Equal(5, disputed.Energy); Assert.Equal(1, disputed.ReturnEnergy); Assert.Equal(["blue"], disputed.CardColorList);
        foreach (var list in new[] { physical, costs })
        {
            var first = list[0];
            Assert.Throws<NotSupportedException>(() => Assert.IsAssignableFrom<IList<OfficialCard>>(list)[0] = disputed);
            Assert.Equal(first, list[0]);
        }
    }
}
