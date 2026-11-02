using Riftbound.CardCatalog;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RadFaceDownPlayParserTests
{
    [Fact]
    public void EvelynnLegendTextParsesItsOncePerTurnDrawAndConditionalDiscard()
    {
        var parsed = Assert.IsType<StandbyPlayedDrawEffect>(StandbyParser.DrawWhenControllerPlaysFromFaceDown(
            "每回合限一次，当你将一张牌从正面朝下的状态打出时，抽一张牌。然后，如果不是你的回合，则弃置一张手牌。"));
        Assert.Equal(1, parsed.DrawCount);
        Assert.Equal(1, parsed.OutsideTurnDiscardCount);
        Assert.True(parsed.OncePerTurn);
    }

    [Theory]
    [InlineData("每回合限一次，当对手将一张牌从正面朝下的状态打出时，抽一张牌。然后，如果不是你的回合，则弃置一张手牌。")]
    [InlineData("每回合限一次，当你将一张牌从手牌打出时，抽一张牌。然后，如果不是你的回合，则弃置一张手牌。")]
    [InlineData("每回合限一次，当你将一张牌正面朝下放置时，抽一张牌。然后，如果不是你的回合，则弃置一张手牌。")]
    [InlineData("当你将一张牌从正面朝下的状态打出时，抽一张牌。然后，如果不是你的回合，则弃置一张手牌。")]
    public void OtherEventsOrMissingTurnLimitDoNotAcquireTheLegendDrawEffect(string text)
        => Assert.Null(StandbyParser.DrawWhenControllerPlaysFromFaceDown(text));
}
