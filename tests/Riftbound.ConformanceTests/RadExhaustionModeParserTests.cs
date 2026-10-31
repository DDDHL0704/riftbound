using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RadExhaustionModeParserTests
{
    private const string Text = "当我变为休眠状态时，选择一个本回合未选过的效果 —\n- 给予我{{增益}}。\n- 消耗我的增益，以此{{眩晕}}一名敌方单位。\n- 消耗我的增益，以此让我变为活跃状态。";

    [Fact]
    public void TheThreeModesDeclareTheirTargetsAndLeadingBoonCosts()
    {
        var spec = Assert.Single(TriggerParser.Parse(Text));
        Assert.Equal(TriggerKinds.SourceExhaustedChooseUnusedMode, spec.Kind);
        Assert.Equal(TriggerTimings.SourceExhausted, spec.Timing); Assert.True(spec.DistinctModesPerTurn);
        var modes = spec.Modes!; Assert.Equal(3, modes.Count);
        Assert.Equal(1, modes[0].BoonCount); Assert.False(modes[0].ConsumeSourceBoon);
        Assert.True(modes[1].ConsumeSourceBoon); Assert.True(modes[1].StunTarget); Assert.Equal(TriggerTargetScopes.EnemyUnitOnField, modes[1].TargetScope);
        Assert.True(modes[2].ConsumeSourceBoon); Assert.True(modes[2].ReadySource); Assert.Equal(TriggerTargetScopes.SourceUnit, modes[2].TargetScope);
    }

    [Theory]
    [InlineData("活跃状态", "休眠状态")]
    [InlineData("当另一名友方单位", "当我")]
    [InlineData("本回合已选过", "本回合未选过")]
    [InlineData("消耗一名友方单位的增益", "消耗我的增益")]
    [InlineData("一名友方单位", "一名敌方单位")]
    public void DifferentEventsParticipantsOrCostsDoNotAcquireTheseModes(string replacement, string original)
        => Assert.DoesNotContain(TriggerParser.Parse(Text.Replace(original, replacement)), t => t.Kind == TriggerKinds.SourceExhaustedChooseUnusedMode);
}
