using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class AttackAreaTriggerParserTests
{
    [Theory]
    [InlineData("当我进攻时，对此处的所有敌方单位各造成3点伤害。", 3, null)]
    [InlineData("当我进攻时，如果你控制的符文数量不超过四枚，则对此处的所有敌方单位各造成2点伤害。", 2, 4)]
    public void ParsesUntargetedAttackDamageAndItsTriggerCondition(string text, int damage, int? maxRunes)
    {
        var trigger = Assert.Single(TriggerParser.Parse(text), t => t.Kind == TriggerKinds.UnitAttackDamageAllEnemyUnitsHere);
        Assert.Equal(TriggerTimings.UnitAttack, trigger.Timing);
        Assert.Equal(TriggerTargetScopes.AllEnemyUnitsHere, trigger.TargetScope);
        Assert.Equal(damage, trigger.DamageAmount);
        Assert.Equal(maxRunes, trigger.MaximumControlledRunes);
    }

    [Fact]
    public void DoesNotTreatAChosenEnemyAsUntargetedAreaDamage()
    {
        Assert.DoesNotContain(TriggerParser.Parse("当我进攻时，选择此处的一名敌方单位，对其造成3点伤害。"),
            trigger => trigger.Kind == TriggerKinds.UnitAttackDamageAllEnemyUnitsHere);
    }
}
