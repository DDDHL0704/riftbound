using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class DependentLastBreathParserTests
{
    [Theory]
    [InlineData("{{绝念}}", false, "三", 3)]
    [InlineData("{{绝念>}}", false, "三", 3)]
    [InlineData("{{绝念>}} —", false, "三", 3)]
    [InlineData("{{已强化>}}{{>绝念>}}", true, "两", 2)]
    [InlineData("{{已强化>}} {{>绝念>}}", true, "两", 2)]
    [InlineData("{{已强化>}}\t{{>绝念>}} —", true, "两", 2)]
    public void MinionProductionRetainsLegacyShapesAndCapturesOnlyTheAdjacentDependentCondition(string prefix, bool empowered, string count, int expectedCount)
    {
        var trigger = Assert.Single(TriggerParser.Parse(prefix + " 打出" + count + "名1{{S}}的“随从”到你的基地。"),
            t => t.Kind == TriggerKinds.UnitLastBreathCreateMinions);
        Assert.Equal(empowered ? true : (bool?)null, trigger.RequiresSourceEmpowered);
        Assert.Equal(expectedCount, trigger.CreatedTokenCount); Assert.Equal("随从", trigger.CreatedTokenName); Assert.Equal(1, trigger.CreatedTokenPower);
        Assert.Equal(TriggerTimings.UnitDestroyed, trigger.Timing); Assert.Equal(TriggerTargetScopes.SourceUnit, trigger.TargetScope);
        Assert.Equal(TriggerTokenDestinations.OwnerBase, trigger.CreatedTokenDestination); Assert.Empty(trigger.CreatedTokenKeywords!);
    }

    [Theory]
    [InlineData("\n")] [InlineData("\r\n")]
    public void AnIndependentEmpoweredSelfClauseDoesNotGateALaterOrdinaryLastBreath(string newline)
    {
        var triggers = TriggerParser.Parse("{{已强化>}} 我获得{{S}}+2。" + newline + "{{绝念>}} 打出两名1{{S}}的“随从”到你的基地。");
        Assert.Null(Assert.Single(triggers, t => t.Kind == TriggerKinds.UnitLastBreathCreateMinions).RequiresSourceEmpowered);
    }

    [Theory]
    [InlineData("{{>绝念>}} 打出两名1{{S}}的“随从”到你的基地。")]
    [InlineData("{{已强化>}} 我获得{{S}}+2。\n{{>绝念>}} 打出两名1{{S}}的“随从”到你的基地。")]
    [InlineData("{{已强化>}}{{>绝念>}} 打出两名1{{S}}的“未知指示物”到你的基地。")]
    public void AnUnboundDependentMarkerOrUnsupportedTokenDoesNotBecomeAnUnconditionalProducer(string text)
    {
        Assert.DoesNotContain(TriggerParser.Parse(text), t => t.Kind == TriggerKinds.UnitLastBreathCreateMinions);
    }

    [Theory]
    [InlineData("OGN·117/298", "on-play")]
    [InlineData("OGN·117a/298", "on-play")]
    [InlineData("OGN·212/298", "on-play")]
    [InlineData("OGN·275/298", TriggerKinds.BattlefieldHeldCreateMinion)]
    public async Task OnPlayAndBattlefieldHeldMinionClausesKeepTheirOriginalTriggerKinds(string no, string expectedKind)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(expectedKind, Assert.Single(TriggerParser.Parse(card.CardEffect)).Kind);
    }
}
