using System.Reflection;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class TypedEmpowerHandDiscardCostTests
{
    // Exact CN VEN·110/id37807, fetched 2026-10-26. This is the printed
    // payment contract only; the empowered banish observer is separate.
    private const string Text = "{{强化}} — 弃置一张法术牌（支付此费用：强化我。仅在未强化时可用。）\n当我变为{{已强化}}时，放逐战场上一名不高于3{{S}}的敌方单位。";

    [Fact]
    public void TheExactPrintedSpellDiscardIsASupportedNonresourceEmpowerCost()
    {
        var cost = Cost();
        Assert.False(cost.SupportedResources);
        Assert.Equal("弃置一张法术牌", cost.AdditionalCost);
        Assert.Equal(0, cost.Mana); Assert.Equal(0, cost.Power); Assert.Empty(cost.TypedPower);
        Assert.False(cost.Exhaust);
        Assert.True(Assert.IsType<bool>(typeof(CoreRuleEngine).Assembly.GetType("Riftbound.Engine.EmpowerRules")!
            .GetMethod("SupportedCost", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [cost])));
    }

    private static EmpowerCostSpec Cost()
        => Assert.Single(Assert.Single(EmpowerParser.Parse(Text)).Alternatives);
}
