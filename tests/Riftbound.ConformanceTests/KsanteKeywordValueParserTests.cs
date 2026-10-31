using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class KsanteKeywordValueParserTests
{
    private const string Text = "当我进攻或防守时，让此处你控制的一名单位在本回合内{{坚守}}数值翻倍。";
    [Fact]
    public void ExactOfficialTextCreatesAFriendlyHereTurnKeywordModifier()
    {
        var spec = Assert.Single(TriggerParser.Parse(Text));
        Assert.Equal(TriggerKinds.UnitAttackOrDefendDoubleFriendlyKeywordHere, spec.Kind);
        Assert.Equal(TriggerTimings.UnitAttackOrDefend, spec.Timing); Assert.Equal(TriggerTargetScopes.FriendlyUnitHere, spec.TargetScope);
        Assert.Equal("坚守", spec.GrantedKeyword); Assert.Equal("UNTIL_END_OF_TURN", spec.Duration); Assert.Null(spec.PowerDelta);
    }
    [Theory, InlineData("进攻或防守", "进攻"), InlineData("你控制", "敌方控制"), InlineData("此处", "任意地点")]
    [InlineData("本回合内", "永久"), InlineData("坚守", "强攻"), InlineData("翻倍", "加一")]
    public void NearMissesCannotAcquireTheNumericKeywordBehavior(string from, string to)
        => Assert.DoesNotContain(TriggerParser.Parse(Text.Replace(from, to, StringComparison.Ordinal)),
            t => t.Kind == TriggerKinds.UnitAttackOrDefendDoubleFriendlyKeywordHere);
}
