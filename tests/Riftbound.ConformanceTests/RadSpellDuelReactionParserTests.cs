using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RadSpellDuelReactionParserTests
{
    [Theory]
    [InlineData("只要我处于法术对决中，你的法术便具有{{反应}}。", true)]
    [InlineData("只要我处于法术对决中, 你的法术便具有{{反应}}.", true)]
    [InlineData("只要我处于战斗中，你的法术便具有{{反应}}。", false)]
    [InlineData("只要我处于法术对决中，敌方的法术便具有{{反应}}。", false)]
    [InlineData("只要我处于法术对决中，你的单位便具有{{反应}}。", false)]
    [InlineData("只要我处于法术对决中，你的法术便具有{{迅捷}}。", false)]
    [InlineData("你的法术便具有{{反应}}。", false)]
    public void OnlyTheExactConditionalSpellPermissionIsAdmitted(string text, bool parsed)
        => Assert.Equal(parsed, StaticAbilityParser.Parse(text, []).Any(a => a.Kind == StaticAbilityKinds.UnitInSpellDuelFriendlySpellsReaction));
}
