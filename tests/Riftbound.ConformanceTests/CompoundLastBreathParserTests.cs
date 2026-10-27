using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class CompoundLastBreathParserTests
{
    [Fact]
    public async Task ExactTastyFaerieTextKeepsBothInstructionsInOneMandatorySkill()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == "OGN·075/298");
        Assert.Equal(31288, card.Id);
        Assert.Equal("{{急速}}（你可以选择额外支付{{1}}和{{绿色}}，让我以活跃状态进场。）\n{{绝念}}—召出两枚休眠的符文，再抽一张牌。（当我被摧毁后，发动此效果。）", card.CardEffect);
        var trigger = Assert.Single(TriggerParser.Parse(card.CardEffect));
        Assert.Equal(TriggerKinds.UnitLastBreathCallRunesThenDraw, trigger.Kind);
        Assert.Equal(TriggerTimings.UnitDestroyed, trigger.Timing); Assert.Equal(TriggerTargetScopes.SourceUnit, trigger.TargetScope);
        Assert.Equal(2, trigger.RuneCallCount); Assert.Equal(1, trigger.DrawCount);
        Assert.False(trigger.Optional ?? false); Assert.Null(trigger.RequiresSourceEmpowered);
    }

    [Theory]
    [InlineData("{{绝念}} 召出两枚休眠的符文。")]
    [InlineData("{{绝念}} 你可以选择召出两枚休眠的符文，再抽一张牌。")]
    [InlineData("{{>绝念>}} 召出两枚休眠的符文，再抽一张牌。")]
    [InlineData("{{已强化>}}{{>绝念>}} 召出两枚休眠的符文，再抽一张牌。")]
    [InlineData("{{绝念}} 召出0枚休眠的符文，再抽一张牌。")]
    [InlineData("{{绝念}} 召出两枚休眠的符文，再抽0张牌。")]
    [InlineData("{{绝念}} 召出两枚休眠的符文，再抽一张牌，再弃一张牌。")]
    public void ScalarOptionalUnboundConditionalAndAdditionalCompoundFormsAreNotClaimed(string text)
        => Assert.DoesNotContain(TriggerParser.Parse(text), t => t.Kind == TriggerKinds.UnitLastBreathCallRunesThenDraw);
}
