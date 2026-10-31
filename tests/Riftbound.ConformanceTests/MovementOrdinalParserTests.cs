using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class MovementOrdinalParserTests
{
    private const string SecondMove = "当我每回合第二次移动时，放逐你主牌堆顶部的一张牌。直到你的下一个回合结束阶段，你可以选择打出该牌。";

    [Theory]
    [InlineData("RAD·015/167")]
    [InlineData("RAD·SP2/005")]
    public async Task BothOfficialTextsGrantOnePaidPlayAfterTheSecondMovement(string cardNo)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, card => card.CardNo == cardNo);
        var trigger = Assert.Single(MovementOrdinalParser.Parse(card.CardEffect),
            trigger => trigger.Kind == TriggerKinds.UnitOrdinalMovementBanishTopPlay);
        Assert.Equal(TriggerTimings.UnitMoved, trigger.Timing);
        Assert.Equal(2, trigger.MovementOrdinal);
        Assert.Equal(1, trigger.BanishCount);
        Assert.Equal(1, trigger.PlayCount);
        Assert.Equal("BANISHED", trigger.PlayOriginZone);
        Assert.Equal(MovementOrdinalParser.NextControllerTurnEnd, trigger.Duration);
        Assert.True(trigger.Optional);
    }

    [Theory]
    [InlineData("")]
    [InlineData("（仍须支付卡牌费用。）")]
    public void TheReminderDoesNotChangeThePermission(string reminder)
    {
        var trigger = Assert.Single(MovementOrdinalParser.Parse(SecondMove + reminder));
        Assert.Equal(TriggerKinds.UnitOrdinalMovementBanishTopPlay, trigger.Kind);
        Assert.Equal(2, trigger.MovementOrdinal);
    }

    [Theory]
    [InlineData("当我每回合第二次", "当我每回合首次")]
    [InlineData("当我每回合第二次", "当我每回合第三次")]
    [InlineData("你主牌堆", "对手主牌堆")]
    [InlineData("你可以选择打出该牌", "你可以选择无视费用打出该牌")]
    public void DifferentOrdinalsOwnersAndFreePlayCannotAcquireThisPermission(string original, string replacement)
        => Assert.Empty(MovementOrdinalParser.Parse(SecondMove.Replace(original, replacement)));
}
