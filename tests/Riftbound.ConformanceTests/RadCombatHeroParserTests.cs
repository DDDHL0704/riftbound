using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RadCombatHeroParserTests
{
    [Theory]
    [InlineData("当我进攻或防守时，对此处的所有敌方单位各造成等同于我战力的伤害。", true)]
    [InlineData("当我进攻或防守时, 对此处的所有敌方单位各造成等同于我战力的伤害.", true)]
    [InlineData("当我进攻时，对此处的所有敌方单位各造成等同于我战力的伤害。", false)]
    [InlineData("当我进攻或防守时，对此处的一名敌方单位造成等同于我战力的伤害。", false)]
    [InlineData("当我进攻或防守时，对此处的所有友方单位各造成等同于我战力的伤害。", false)]
    [InlineData("当我进攻或防守时，对此处的所有敌方单位各造成4点伤害。", false)]
    public void OnlyTheExactUntargetedCombatMightDamageRuleIsAdmitted(string text, bool parsed)
        => Assert.Equal(parsed, TriggerParser.Parse(text).Any(t => t.Kind == TriggerKinds.UnitAttackOrDefendSourceMightDamageAllEnemyUnitsHere));

    [Theory]
    [InlineData("除非此处有一件友方装备，否则我无法造成战斗伤害。", true)]
    [InlineData("除非此处有一件友方装备, 否则我无法造成战斗伤害.", true)]
    [InlineData("除非此处有一件敌方装备，否则我无法造成战斗伤害。", false)]
    [InlineData("除非此处有一件友方装备，否则我无法造成伤害。", false)]
    [InlineData("除非此处有一件友方武器，否则我无法造成战斗伤害。", false)]
    public void OnlyTheExactEquipmentConditionGatesCombatDamage(string text, bool parsed)
        => Assert.Equal(parsed, StaticAbilityParser.Parse(text, []).Any(a => a.Kind == StaticAbilityKinds.UnitFriendlyEquipmentHereCombatDamage));
}
