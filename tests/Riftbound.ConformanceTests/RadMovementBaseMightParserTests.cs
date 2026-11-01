using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RadMovementBaseMightParserTests
{
    [Theory]
    [InlineData(0)] [InlineData(4)] [InlineData(7)]
    public void ParsesTheInstructionByTextRatherThanCardIdentity(int might)
    {
        var spec = Assert.Single(TriggerParser.Parse($"当我移动时，你可以选择让另一名单位的基础战力在本回合内变为{might}。"));
        Assert.Equal(TriggerKinds.UnitMovedSetOtherUnitBaseMight, spec.Kind);
        Assert.Equal(TriggerTimings.UnitMoved, spec.Timing); Assert.True(spec.Optional);
        Assert.Equal(TriggerTargetScopes.OtherUnitOnField, spec.TargetScope);
        Assert.Equal(TriggerDurations.UntilEndOfTurn, spec.Duration); Assert.Equal(might, spec.BasePowerUntilEndOfTurn);
    }

    [Theory]
    [InlineData("当我进攻时，你可以选择让另一名单位的基础战力在本回合内变为4。")]
    [InlineData("当我移动时，你可以选择让一名单位的基础战力在本回合内变为4。")]
    [InlineData("当我移动时，你可以选择让另一名友方单位的基础战力在本回合内变为4。")]
    [InlineData("当我移动时，你可以选择让另一名单位的战力在本回合内变为4。")]
    [InlineData("当我移动时，你可以选择让另一名单位的基础战力变为4。")]
    [InlineData("当我移动时，你可以选择让另一名单位的基础战力在本回合内变为-1。")]
    [InlineData("当我移动时，你可以选择让另一名单位的基础战力在本回合内变为2147483648。")]
    public void SimilarInstructionsDoNotAcquireThisImplementedBehavior(string text)
        => Assert.DoesNotContain(TriggerParser.Parse(text), t => t.Kind == TriggerKinds.UnitMovedSetOtherUnitBaseMight);
}
