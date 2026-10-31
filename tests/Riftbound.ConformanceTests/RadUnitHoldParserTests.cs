using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RadUnitHoldParserTests
{
    [Theory]
    [InlineData("当我据守一处战场时，召出一枚符文。", TriggerKinds.UnitBattlefieldHeldCallRune)]
    [InlineData("当我据守一处战场时，如果你在此处控制着正面朝下的卡牌，则获得1分。", TriggerKinds.UnitBattlefieldHeldScore)]
    public void ExactPrintedTextIsParsedWithOrWithoutTheSentenceTerminator(string text, string kind)
    {
        foreach (var version in new[] { text, text.TrimEnd('。') }) {
            var spec = Assert.Single(TriggerParser.Parse(version));
            Assert.Equal(kind, spec.Kind); Assert.Equal(TriggerTimings.BattlefieldHeld, spec.Timing);
            Assert.Equal(TriggerTargetScopes.SourceUnit, spec.TargetScope);
        }
    }

    [Theory]
    [InlineData("当你据守一处战场时，召出一枚符文。")]
    [InlineData("当我据守一处战场时，召出两枚符文。")]
    [InlineData("当我据守一处战场时，召出一枚休眠的符文。")]
    [InlineData("当我征服一处战场时，召出一枚符文。")]
    [InlineData("当我据守一处战场时，如果你在任意地点控制着正面朝下的卡牌，则获得1分。")]
    [InlineData("当我据守一处战场时，如果对手在此处控制着正面朝下的卡牌，则获得1分。")]
    [InlineData("当我据守一处战场时，如果你在此处控制着正面朝上的卡牌，则获得1分。")]
    [InlineData("当我据守一处战场时，如果你在此处控制着正面朝下的卡牌，则获得2分。")]
    public void DifferentTimingControllerVisibilityAndAmountsCannotAcquireEitherImplementedHold(string text)
        => Assert.DoesNotContain(TriggerParser.Parse(text), t => t.Kind == TriggerKinds.UnitBattlefieldHeldCallRune
            || t.Kind == TriggerKinds.UnitBattlefieldHeldScore && t.RequiresControlledFaceDownCardHere == true);
}
