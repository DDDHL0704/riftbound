using Riftbound.CardCatalog;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class VenReviewedErrataTests
{
    // CN VEN errata pp. 4-5, 7-8 (2026-07-24), SHA256
    // 425625f2e1a0747358ac23f3f700cae813922fba4ee7b7455343038a1621bb5b.
    [Theory]
    [InlineData("VEN·044", 37983, "则你下一张卡牌的费用减少", "则你在本回合内打出的下一张卡牌的费用减少")]
    [InlineData("VEN·034", 37986, "{{反应}}（可在你的回合或法术对决中打出。）", "{{反应}}（可在任意时机打出，甚至先于其他法术和技能的结算。）")]
    [InlineData("VEN·079", 37773, "选择此处一名战力大于我的单位", "选择此处的一名单位")]
    public async Task RuntimeAppliesTheReviewedErratumWithoutChangingTheAcquiredFace(string no, long id, string oldText, string currentText)
    {
        var acquired = Assert.Single(VendettaCardSources.GetOfficialCards(), card => card.Id == id && card.CardNo == no);
        var admitted = Assert.Single(VendettaCardSources.GetAdmittedCards(), card => card.Id == id && card.CardNo == no);
        var runtime = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, card => card.Id == id && card.CardNo == no);
        Assert.Contains(oldText, acquired.CardEffect, StringComparison.Ordinal);
        Assert.Equal(acquired.CardEffect.Replace(oldText, currentText, StringComparison.Ordinal), admitted.CardEffect);
        Assert.Equal(admitted.CardEffect, runtime.CardEffect);
        Assert.DoesNotContain(oldText, runtime.CardEffect, StringComparison.Ordinal);
        Assert.Equal(acquired.Energy, runtime.Energy);
        Assert.Equal(acquired.ReturnEnergy, runtime.ReturnEnergy);
        Assert.Equal(acquired.Power, runtime.Power);
    }
}
