using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RadExhaustionDrawParserTests
{
    [Theory]
    [InlineData("每回合限一次，当你同时让不少于三名友方单位变为休眠状态时，你可以选择抽一张牌。", 3, 1)]
    [InlineData("每回合限一次，当你同时让不少于4名友方单位变为休眠状态时，你可以选择抽2张牌", 4, 2)]
    public void ParsesInstructionThresholdAndOptionalOncePerTurnDraw(string text, int units, int draws)
    {
        var spec = Assert.Single(TriggerParser.Parse(text), t => t.Kind == TriggerKinds.ControllerExhaustedUnitGroupDraw);
        Assert.Equal(TriggerTimings.ControllerExhaustedUnitGroup, spec.Timing);
        Assert.Equal(units, spec.RequiredUnitCount); Assert.Equal(draws, spec.DrawCount);
        Assert.True(spec.Optional); Assert.True(spec.OncePerTurn);
    }

    [Theory]
    [InlineData("每回合限一次，当你让不少于三名友方单位变为休眠状态时，你可以选择抽一张牌。")]
    [InlineData("每回合限一次，当你同时让不少于三名敌方单位变为休眠状态时，你可以选择抽一张牌。")]
    [InlineData("每回合限一次，当你同时让不少于三件友方装备变为休眠状态时，你可以选择抽一张牌。")]
    [InlineData("每回合限一次，当你同时让不少于三名友方单位进场时，你可以选择抽一张牌。")]
    public void OtherEventsDoNotAcquireTheSimultaneousFriendlyExhaustionRule(string text)
        => Assert.DoesNotContain(TriggerParser.Parse(text), t => t.Kind == TriggerKinds.ControllerExhaustedUnitGroupDraw);
}
