using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class EquipmentKeywordParserTests
{
    private const string Aura = "你拥有{{部署}}的装备具有{{迅捷}}。";
    private const string Conquest = "当我征服一处战场时，如果此处有拥有{{部署}}的友方装备，则召出一枚休眠的符文。";

    [Theory]
    [InlineData("")]
    [InlineData("（其可在你的回合或法术对决中打出。）")]
    public void ReminderTextDoesNotRestrictTheOwnedEquipmentAuraToTheField(string reminder)
    {
        var spec = Assert.Single(StaticAuraParser.Parse(Aura + reminder + "\n" + Conquest));
        Assert.Equal(StaticAuraKinds.OwnedFilteredEquipmentKeyword, spec.Kind);
        Assert.Equal("RULE_TEXT", spec.Layer);
        Assert.Equal(StaticAuraTargetScopes.OwnedFilteredEquipment, spec.TargetScope);
        Assert.Equal(StaticAuraTargetFilters.TagPrefix + "部署", spec.TargetFilter);
        Assert.Equal("迅捷", spec.GrantedKeyword);
    }

    [Fact]
    public void ConquestRequiresFriendlyDeployEquipmentHereAndCallsOneDormantRune()
    {
        var spec = Assert.Single(TriggerParser.Parse(Aura + "\n" + Conquest));
        Assert.Equal(TriggerKinds.UnitConquestCallDormantRune, spec.Kind);
        Assert.Equal(TriggerTimings.UnitConquest, spec.Timing);
        Assert.Equal(TriggerTargetScopes.SourceUnit, spec.TargetScope);
        Assert.Equal("部署", spec.RequiresFriendlyEquipmentKeywordHere);
        Assert.Equal(1, spec.RuneCallCount);
    }

    [Theory]
    [InlineData("你拥有", "对手拥有")]
    [InlineData("你拥有", "你控制")]
    [InlineData("部署", "装配")]
    [InlineData("迅捷", "反应")]
    public void DifferentOwnersFiltersAndPermissionsDoNotAcquireThisAura(string before, string after)
        => Assert.Empty(EquipmentKeywordParser.ParseOwnedKeywordGrants(Aura.Replace(before, after)));

    [Theory]
    [InlineData("我征服", "你征服")]
    [InlineData("征服", "据守")]
    [InlineData("此处", "任意地点")]
    [InlineData("友方", "敌方")]
    [InlineData("休眠的", "活跃的")]
    [InlineData("一枚", "两枚")]
    public void DifferentTimingLocationControllerAndRuneInstructionRemainDistinct(string before, string after)
        => Assert.Empty(EquipmentKeywordParser.ParseConquest(Conquest.Replace(before, after)));
}
