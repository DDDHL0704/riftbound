using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class BattlefieldStaticSourceIdentityGuardTests
{
    [Theory]
    [InlineData("OGN·084/298", "EAGER_APPRENTICE_SPELL_COST_STATIC_PLAY_UNIT")]
    public void CardBehaviorRegistryIdentifiesBattlefieldStaticSourceUnitsByEffectKind(
        string cardNo,
        string effectKind)
    {
        Assert.True(CardBehaviorRegistry.IsImplementedUnitWithEffectKind(cardNo, effectKind));
    }

    [Theory]
    [InlineData("OGN·084/298", "BILGEWATER_BULLY_NO_BOON_ROAM_PLAY_UNIT")]
    [InlineData("OGN·125/298", "EAGER_APPRENTICE_SPELL_COST_STATIC_PLAY_UNIT")]
    public void CardBehaviorRegistryRejectsNonMatchingBattlefieldStaticSourceUnits(
        string cardNo,
        string effectKind)
    {
        Assert.False(CardBehaviorRegistry.IsImplementedUnitWithEffectKind(cardNo, effectKind));
    }

    [Fact]
    public void EagerApprenticeSpellCostSourceIdentityUsesBehaviorFields()
    {
        var coreRuleEngineSource = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs"));
        var matchSessionSource = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "MatchSession.cs"));

        Assert.DoesNotContain("EagerApprenticeCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("EagerApprenticeCardNo", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Equals(cardObject.CardNo, EagerApprenticeCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Equals(cardObject.CardNo, EagerApprenticeCardNo", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("EagerApprenticeSpellCostStaticSourceEffectKind", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("EagerApprenticeSpellCostStaticSourceEffectKind", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("EAGER_APPRENTICE_SPELL_COST_STATIC_PLAY_UNIT", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("EAGER_APPRENTICE_SPELL_COST_STATIC_PLAY_UNIT", matchSessionSource, StringComparison.Ordinal);
        Assert.Contains("StaticSpellCostReductionMana", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("StaticSpellCostReductionMinimumManaCost", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("StaticSpellCostReductionMana", matchSessionSource, StringComparison.Ordinal);
        Assert.Contains("StaticSpellCostReductionMinimumManaCost", matchSessionSource, StringComparison.Ordinal);
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
