using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RadAttackRallyParserTests
{
    [Theory]
    [InlineData("当我进攻时，你可以选择将你控制的一名单位移动到我所在的战场。", true)]
    [InlineData("当我进攻时，你可以选择将你控制的一名单位移动到我的战场。", true)]
    [InlineData("当我进攻时, 你可以选择将你控制的一名单位移动到我的战场.", true)]
    [InlineData("当我防守时，你可以选择将你控制的一名单位移动到我所在的战场。", false)]
    [InlineData("当我进攻或防守时，你可以选择将你控制的一名单位移动到我的战场。", false)]
    [InlineData("当我进攻时，你可以选择将一名敌方单位移动到我所在的战场。", false)]
    [InlineData("当我进攻时，你可以选择将你控制的一件装备移动到我的战场。", false)]
    [InlineData("当我进攻时，你可以选择将你控制的所有单位移动到我的战场。", false)]
    [InlineData("当我进攻时，将你控制的一名单位移动到我的战场。", false)]
    [InlineData("当我进攻时，你可以选择将你控制的一名单位移动到我的基地。", false)]
    public void OnlyTheOfficialOptionalAttackMovementIsAdmitted(string text, bool expected)
        => Assert.Equal(expected, TriggerParser.Parse(text).Any(t => t.Kind == TriggerKinds.UnitAttackMoveControlledUnitToSourceBattlefield));
}
