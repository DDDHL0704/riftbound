using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class PlayBehaviorSourceIdentityGuardTests
{
    [Theory]
    [InlineData("OGN·031/298", "RAGING_DRAKE_NEXT_SPELL_COST_PLAY_UNIT")]
    public void CardBehaviorRegistryIdentifiesPlaySourceUnitsByEffectKind(
        string cardNo,
        string effectKind)
    {
        Assert.True(CardBehaviorRegistry.IsImplementedUnitWithEffectKind(cardNo, effectKind));
    }

    [Theory]
    [InlineData("OGN·031/298", "EAGER_APPRENTICE_SPELL_COST_STATIC_PLAY_UNIT")]
    [InlineData("OGN·084/298", "RAGING_DRAKE_NEXT_SPELL_COST_PLAY_UNIT")]
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
