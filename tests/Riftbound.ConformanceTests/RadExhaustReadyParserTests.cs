using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RadExhaustReadyParserTests
{
    [Theory]
    [InlineData(1)] [InlineData(3)]
    public void ParsesOtherFriendlyExhaustionAndItsTurnMight(int amount)
    {
        var trigger = Assert.Single(TriggerParser.Parse($"当另一名友方单位变为休眠状态时，给予我在本回合内{{{{S}}}}+{amount}。"),
            t => t.Kind == TriggerKinds.OtherFriendlyUnitExhaustedSourcePower);
        Assert.Equal("OTHER_FRIENDLY_UNIT_EXHAUSTED", trigger.Timing);
        Assert.Equal(TriggerTargetScopes.SourceUnit, trigger.TargetScope);
        Assert.Equal(amount, trigger.PowerDelta); Assert.Equal(TriggerDurations.UntilEndOfTurn, trigger.Duration);
    }

    [Fact]
    public void ParsesTheRequiredFriendlyExhaustionCostAndThePerTurnRestriction()
    {
        var ability = Assert.Single(ActivatedAbilityParser.Parse(
            "让一名友方单位变为休眠状态：让一名单位变为活跃状态。此技能每回合仅能使用一次。"));
        Assert.Equal(ActivatedAbilityKinds.ExhaustFriendlyUnitReadyUnit, ability.Kind);
        Assert.Equal(CardTargetScopes.AnyUnit, ability.TargetScope); Assert.Equal(1, ability.RequiredTargetCount); Assert.True(ability.OncePerTurn);
    }

    [Theory]
    [InlineData("当另一名敌方单位变为休眠状态时，给予我在本回合内{{S}}+1。")]
    [InlineData("当我变为休眠状态时，给予我在本回合内{{S}}+1。")]
    [InlineData("当另一名友方单位变为活跃状态时，给予我在本回合内{{S}}+1。")]
    public void OtherParticipantsAndEventsDoNotAcquireThisTrigger(string text)
        => Assert.DoesNotContain(TriggerParser.Parse(text), t => t.Kind == TriggerKinds.OtherFriendlyUnitExhaustedSourcePower);

    [Theory]
    [InlineData("让一名敌方单位变为休眠状态：让一名单位变为活跃状态。此技能每回合仅能使用一次。")]
    [InlineData("让一名友方单位变为休眠状态：让一名单位变为活跃状态。")]
    [InlineData("让一名友方单位变为休眠状态：让一件装备变为活跃状态。此技能每回合仅能使用一次。")]
    public void DifferentCostsTargetsAndRestrictionsDoNotAcquireThisAbility(string text)
        => Assert.DoesNotContain(ActivatedAbilityParser.Parse(text), a => a.Kind == ActivatedAbilityKinds.ExhaustFriendlyUnitReadyUnit);
}
