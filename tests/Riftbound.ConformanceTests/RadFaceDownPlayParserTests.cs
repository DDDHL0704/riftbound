using Riftbound.CardCatalog;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RadFaceDownPlayParserTests
{
    [Theory]
    [InlineData("每回合限一次，当对手将一张牌从正面朝下的状态打出时，抽一张牌。然后，如果不是你的回合，则弃置一张手牌。")]
    [InlineData("每回合限一次，当你将一张牌从手牌打出时，抽一张牌。然后，如果不是你的回合，则弃置一张手牌。")]
    [InlineData("每回合限一次，当你将一张牌正面朝下放置时，抽一张牌。然后，如果不是你的回合，则弃置一张手牌。")]
    [InlineData("当你将一张牌从正面朝下的状态打出时，抽一张牌。然后，如果不是你的回合，则弃置一张手牌。")]
    public void DifferentEventsOrMissingTurnLimitsDoNotAcquireTheLegendDrawEffect(string text)
        => Assert.Null(StandbyParser.DrawWhenControllerPlaysFromFaceDown(text));

    [Theory]
    [InlineData("当你将一张卡牌从正面朝下的状态打出时，让我变为活跃状态，且给予我在本回合内{{S}}+1。", 1, true)]
    [InlineData("{{伏击}}\n当你将一张卡牌从正面朝下的状态打出时，让我变为活跃状态，且给予我在本回合内{{S}}+2。", 2, true)]
    [InlineData("每当你将一张牌从正面朝下的{{待命}}状态中打出时，让我本回合内{{S}}+2。", 2, false)]
    public void BothPrintedWordingsRetainTheIndependentReadyInstruction(string text, int might, bool ready)
    {
        var parsed = Assert.IsType<StandbyPlayedSelfEffect>(StandbyParser.SelfEffectWhenControllerPlaysFromFaceDown(text));
        Assert.Equal(might, parsed.Might); Assert.Equal(ready, parsed.ReadiesSelf);
        Assert.Equal(might, StandbyParser.SelfMightWhenControllerPlaysFromFaceDown(text));
    }

    [Theory]
    [InlineData("当你将一张卡牌从手牌打出时，让我变为活跃状态，且给予我在本回合内{{S}}+1。")]
    [InlineData("当对手将一张卡牌从正面朝下的状态打出时，让我变为活跃状态，且给予我在本回合内{{S}}+1。")]
    [InlineData("当你将一张卡牌正面朝下放置时，让我变为活跃状态，且给予我在本回合内{{S}}+1。")]
    [InlineData("当你将一张卡牌从正面朝下的状态打出时，让另一名单位变为活跃状态，且给予我在本回合内{{S}}+1。")]
    public void DifferentEventsControllersAndRecipientsAreNotAdmitted(string text)
        => Assert.Null(StandbyParser.SelfEffectWhenControllerPlaysFromFaceDown(text));
}
