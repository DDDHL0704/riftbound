using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class PlayBehaviorSourceIdentityGuardTests
{
    [Theory]
    [InlineData("OGN·031/298", "RAGING_DRAKE_NEXT_SPELL_COST_PLAY_UNIT")]
    [InlineData("OGN·061/298", "PORO_HERDER_NO_PORO_STATIC_PLAY_UNIT")]
    [InlineData("UNL-097/219", "BALANCED_DISCIPLE_NO_OTHER_POWER_VANILLA_PLAY_UNIT")]
    public void CardBehaviorRegistryIdentifiesPlaySourceUnitsByEffectKind(
        string cardNo,
        string effectKind)
    {
        Assert.True(CardBehaviorRegistry.IsImplementedUnitWithEffectKind(cardNo, effectKind));
    }

    [Theory]
    [InlineData("OGN·031/298", "EAGER_APPRENTICE_SPELL_COST_STATIC_PLAY_UNIT")]
    [InlineData("OGN·084/298", "RAGING_DRAKE_NEXT_SPELL_COST_PLAY_UNIT")]
    [InlineData("OGN·061/298", "RAGING_DRAKE_NEXT_SPELL_COST_PLAY_UNIT")]
    [InlineData("OGN·031/298", "PORO_HERDER_NO_PORO_STATIC_PLAY_UNIT")]
    [InlineData("UNL-097/219", "PORO_HERDER_NO_PORO_STATIC_PLAY_UNIT")]
    [InlineData("OGN·061/298", "BALANCED_DISCIPLE_NO_OTHER_POWER_VANILLA_PLAY_UNIT")]
    public void CardBehaviorRegistryRejectsNonMatchingPlaySourceUnits(
        string cardNo,
        string effectKind)
    {
        Assert.False(CardBehaviorRegistry.IsImplementedUnitWithEffectKind(cardNo, effectKind));
    }

    [Fact]
    public void RagingDrakeNextSpellCostPlaySourceUsesCatalogEffectKind()
    {
        var coreRuleEngineSource = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs"));

        Assert.DoesNotContain("RagingDrakeCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Equals(behavior.CardNo, RagingDrakeCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("RagingDrakeNextSpellCostSourceEffectKind", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains(
            "string.Equals(behavior.EffectKind, RagingDrakeNextSpellCostSourceEffectKind",
            coreRuleEngineSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PoroHerderBoonDrawPlaySourceUsesCatalogEffectKind()
    {
        var coreRuleEngineSource = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs"));

        Assert.DoesNotContain("PoroHerderCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Equals(behavior.CardNo, PoroHerderCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("PoroHerderBoonDrawSourceEffectKind", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains(
            "string.Equals(behavior.EffectKind, PoroHerderBoonDrawSourceEffectKind",
            coreRuleEngineSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void BalancedDiscipleOtherPowerDrawPlaySourceUsesCatalogEffectKind()
    {
        var coreRuleEngineSource = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs"));

        Assert.DoesNotContain("BalancedDiscipleCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Equals(behavior.CardNo, BalancedDiscipleCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("BalancedDiscipleOtherPowerDrawSourceEffectKind", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains(
            "string.Equals(behavior.EffectKind, BalancedDiscipleOtherPowerDrawSourceEffectKind",
            coreRuleEngineSource,
            StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "riftbound-dotnet.sln"))
                || File.Exists(Path.Combine(current.FullName, "Riftbound.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root from test output directory.");
    }
}
