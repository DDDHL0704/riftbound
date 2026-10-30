using System.Reflection;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class TokenDefinitionAuditTests
{
    // Independent expectations from current CN core 187.1-187.7, including
    // older printings whose gallery tag fields have not been updated.
    [Theory]
    [InlineData("OGN·271/298", 1, "随从")]
    [InlineData("OGN·272/298", 1, "随从")]
    [InlineData("OGN·273/298", 1, "随从")]
    [InlineData("OGN·274/298", 3, "仙灵")]
    [InlineData("UNL·T07", 3, "仙灵")]
    [InlineData("SFD·T02", 2, "恕瑞玛")]
    [InlineData("SFD·T01", 3, "机械")]
    [InlineData("UNL·T02", 1, "鸟类")]
    public void OfficialTokenTagsAlsoMatchTheGenericTagFilter(string card, int might, string tag)
    {
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(card, out var factory));
        var token = factory.CreateObject("TOKEN", "P1", "P1", isExhausted: true);
        Assert.Equal(might, token.Power); Assert.Contains(tag, token.Tags); Assert.Equal(card, token.TokenFactoryCardNo);
        var aura = new StaticAuraSpec("test", "", "", "", "", 0, "", "", "", TargetFilter: "TAG:" + tag);
        var rules = typeof(CoreRuleEngine).Assembly.GetType("Riftbound.Engine.StaticAuraSpecRules")!;
        var matcher = rules.GetMethod("TargetMatchesFilter", BindingFlags.Public | BindingFlags.Static, [typeof(StaticAuraSpec), typeof(CardObjectState)])!;
        Assert.True((bool)matcher.Invoke(null, [aura, token])!);
        Assert.False((bool)matcher.Invoke(null, [aura with { TargetFilter = "TAG:龙" }, token])!);
    }

    [Theory]
    [InlineData("OGN·265/298", "随从")]
    [InlineData("SFD·197/221", "恕瑞玛")]
    [InlineData("UNL-189/219", "仙灵")]
    public async Task RealLegendCreationPreservesTheCoreDefinedTag(string card, string tag)
    {
        var cast = await OfficialLegendTokenTests.Activate(OfficialLegendTokenTests.Position(card, 0), card);
        var done = await OfficialTokenReplacementTests.ConfirmBaseTokenLocations(await Top(cast.State)); var token = Assert.Single(OfficialTokenReplacementTests.Tokens(done.State));
        Assert.Contains(tag, token.Tags); Restore(done.State);
    }

    [Fact]
    public async Task SpellSpriteCreationUsesTheSameCoreDefinedTag()
    {
        var s = OfficialTokenReplacementTests.Position(0, "OGN·094/298");
        var cast = await Act(s, "P1", new PlayCardCommand("C", "OGN·094/298", []));
        var done = await OfficialTokenReplacementTests.ConfirmBaseTokenLocations(await Top(cast.State)); var token = Assert.Single(OfficialTokenReplacementTests.Tokens(done.State));
        Assert.Contains("仙灵", token.Tags); Assert.Contains(CardObjectTags.Ephemeral, token.Tags); Restore(done.State);
    }
}
