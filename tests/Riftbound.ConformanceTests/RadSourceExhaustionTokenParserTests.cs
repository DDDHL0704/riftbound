using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RadSourceExhaustionTokenParserTests
{
    [Theory]
    [InlineData("当你打出我时，或当我变为休眠状态时，打出一名1{{S}}的“随从”单位指示物到你的基地。", 1, 1, "随从")]
    [InlineData("当你打出我时，或当我变为休眠状态时，打出两名3{{S}}的“机器人”单位指示物到你的基地。", 2, 3, "机器人")]
    public void ParsesTheTwoAlternativeSourceEventsAndThePrintedTokenInstruction(string text, int count, int might, string family)
    {
        var t = Assert.Single(TriggerParser.Parse(text), t => t.Kind == TriggerKinds.SourceUnitPlayedOrExhaustedCreateUnit);
        Assert.Equal(TriggerTimings.SourceUnitPlayedOrExhausted, t.Timing); Assert.Equal(TriggerTargetScopes.SourceUnit, t.TargetScope);
        Assert.Equal(count, t.CreatedTokenCount); Assert.Equal(might, t.CreatedTokenPower); Assert.Equal(family, t.CreatedTokenName);
        Assert.Equal(TriggerTokenDestinations.OwnerBase, t.CreatedTokenDestination); Assert.True(t.CreatedTokenExhausted);
    }

    [Theory]
    [InlineData("当你打出我时，或当我变为活跃状态时，打出一名1{{S}}的“随从”单位指示物到你的基地。")]
    [InlineData("当你打出我时，或当其他单位变为休眠状态时，打出一名1{{S}}的“随从”单位指示物到你的基地。")]
    [InlineData("当你打出我时，或当我变为休眠状态时，打出一名1{{S}}的“随从”单位指示物到此处。")]
    public void DifferentTimingSourcesAndDestinationsDoNotAcquireTheBaseTokenRule(string text)
        => Assert.DoesNotContain(TriggerParser.Parse(text), t => t.Kind == TriggerKinds.SourceUnitPlayedOrExhaustedCreateUnit);

    [Theory]
    [InlineData("你每控制一名其他休眠的单位，我便具有{{S}}+1。", 1)]
    [InlineData("你每控制一名其他休眠的单位，我便具有{{S}}+3。", 3)]
    public void ParsesTheCurrentOtherExhaustedFriendlyParticipantFilter(string text, int amount)
    {
        var aura = Assert.Single(StaticAuraParser.Parse(text));
        Assert.Equal(StaticAuraKinds.FriendlyFilteredUnitCountToSourcePower, aura.Kind);
        Assert.Equal(StaticAuraParticipantScopes.OtherFriendlyFilteredPublicUnits, aura.ParticipantScope);
        Assert.Equal(StaticAuraTargetScopes.SourceObject, aura.TargetScope); Assert.Equal(StaticAuraTargetFilters.Exhausted, aura.TargetFilter);
        Assert.Equal(amount, aura.PowerDeltaPerParticipant);
    }

    [Theory]
    [InlineData("你每控制一名其他活跃的单位，我便具有{{S}}+1。")]
    [InlineData("你每控制一件其他休眠的装备，我便具有{{S}}+1。")]
    public void UnrelatedParticipantStatesAndKindsDoNotAcquireTheExhaustedUnitAura(string text)
        => Assert.DoesNotContain(StaticAuraParser.Parse(text), a => a.ParticipantScope == StaticAuraParticipantScopes.OtherFriendlyFilteredPublicUnits);
}
