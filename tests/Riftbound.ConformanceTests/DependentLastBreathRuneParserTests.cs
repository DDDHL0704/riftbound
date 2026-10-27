using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class DependentLastBreathRuneParserTests
{
    private const string Text = "{{强化1AA}}（支付{{1}}和{{A}}{{A}}：强化我。仅在未强化时可用。）\n{{已强化>}} 我获得{{S}}+2。\n{{已强化>}}{{>绝念>}} 召出两枚休眠的符文。（当我在已强化状态下被摧毁时，发动此效果。）";

    [Fact]
    public void ExactCn37772NativeCostAndSelfBonusRemainIndependentOfTheDeathEffect()
    {
        var cost = Assert.Single(Assert.Single(EmpowerParser.Parse(Text)).Alternatives);
        Assert.Equal(1, cost.Mana); Assert.Equal(2, cost.Power); Assert.Empty(cost.TypedPower);
        Assert.True(cost.SupportedResources); Assert.False(cost.Exhaust); Assert.Empty(cost.AdditionalCost);
        var self = EmpowerParser.Self(Text); Assert.Equal(2, self.Might); Assert.Empty(self.Keywords);
    }

    [Fact]
    public void ExactCn37772DependentLastBreathCapturesTwoDormantRunesAndItsAdjacentCondition()
    {
        var trigger = Assert.Single(TriggerParser.Parse(Text), t => t.Kind == TriggerKinds.UnitLastBreathCallRuneOne);
        Assert.Equal(TriggerTimings.UnitDestroyed, trigger.Timing); Assert.Equal(TriggerTargetScopes.SourceUnit, trigger.TargetScope);
        Assert.Equal(2, trigger.RuneCallCount); Assert.True(trigger.RequiresSourceEmpowered);
        Assert.Null(trigger.DrawCount); Assert.False(trigger.Optional ?? false);
    }

    [Theory]
    [InlineData("{{绝念}}")] [InlineData("{{绝念>}}")]
    [InlineData("{{绝念}}—")] [InlineData("{{绝念>}} —")]
    public void LegacyOneRuneMarkersKeepTheirCountAndUnconditionalStatus(string prefix)
    {
        var trigger = Assert.Single(TriggerParser.Parse(prefix + " 召出一枚休眠的符文。"), t => t.Kind == TriggerKinds.UnitLastBreathCallRuneOne);
        Assert.Equal(1, trigger.RuneCallCount); Assert.Null(trigger.RequiresSourceEmpowered);
    }

    [Theory]
    [InlineData("{{已强化>}}{{>绝念>}}", "两")]
    [InlineData("{{已强化>}} {{>绝念>}}", "二")]
    [InlineData("{{已强化>}}\t{{>绝念>}} —", "2")]
    public void ExplicitAdjacentDependentMarkersRetainTheActualPositiveRuneCount(string prefix, string count)
    {
        var trigger = Assert.Single(TriggerParser.Parse(prefix + " 召出" + count + "枚休眠的符文。"), t => t.Kind == TriggerKinds.UnitLastBreathCallRuneOne);
        Assert.Equal(2, trigger.RuneCallCount); Assert.True(trigger.RequiresSourceEmpowered);
        Assert.Null(trigger.DrawCount); Assert.False(trigger.Optional ?? false);
    }

    [Theory]
    [InlineData("\n")] [InlineData("\r\n")]
    public void AnEarlierIndependentEmpoweredClauseDoesNotGateAnOrdinaryTwoRuneLastBreath(string newline)
    {
        var trigger = Assert.Single(TriggerParser.Parse("{{已强化>}} 我获得{{S}}+2。" + newline + "{{绝念}} 召出两枚休眠的符文。"),
            t => t.Kind == TriggerKinds.UnitLastBreathCallRuneOne);
        Assert.Equal(2, trigger.RuneCallCount); Assert.Null(trigger.RequiresSourceEmpowered);
    }

    [Theory]
    [InlineData("{{>绝念>}} 召出两枚休眠的符文。")]
    [InlineData("{{已强化>}} 我获得{{S}}+2。\n{{>绝念>}} 召出两枚休眠的符文。")]
    [InlineData("{{已强化>}}{{>绝念>}} 召出两枚休眠的符文，再抽一张牌。")]
    [InlineData("{{已强化>}}{{>绝念>}} 召出0枚休眠的符文。")]
    [InlineData("{{已强化>}}{{>绝念>}} 你可以选择召出两枚休眠的符文。")]
    public void UnboundConditionalOptionalCompoundAndZeroFormsAreNotClaimedAsTheScalarRuneProducer(string text)
    {
        Assert.DoesNotContain(TriggerParser.Parse(text), t => t.Kind == TriggerKinds.UnitLastBreathCallRuneOne);
    }

    [Fact]
    public async Task TheExistingTwoRunePlusDrawPrintingIsNotReclassifiedAsTheRuneOnlyEffect()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == "OGN·075/298");
        Assert.Contains("召出两枚休眠的符文，再抽一张牌", card.CardEffect);
        Assert.DoesNotContain(TriggerParser.Parse(card.CardEffect), t => t.Kind == TriggerKinds.UnitLastBreathCallRuneOne);
    }
}
