using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class LegendActionSourceIdentityGuardTests
{
    [Fact]
    public void CoreLegendActionSourceIdentityDoesNotUseDuplicatedCardNumberHelpers()
    {
        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain("IsAzirLegendCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsEzrealLegendCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsTeemoLegendCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsIreliaLegendCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("LegendCardHasAbility", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("AzirLegendAbilityId", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("EzrealLegendAbilityId", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("TeemoLegendAbilityId", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("IreliaLegendAbilityId", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("TryGetLegendAbility", coreRuleEngineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void CoreLegendIdentitySourceDoesNotUseDuplicatedCardNumberHelpers()
    {
        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain("IsRengarLegendCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsLeonaLegendCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsSivirLegendCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsJhinLegendCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("LegendCardHasIdentity", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("RengarLegendIdentityId", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("LeonaLegendIdentityId", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("SivirLegendIdentityId", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("JhinLegendIdentityId", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("TryGetLegendIdentity", coreRuleEngineSource, StringComparison.Ordinal);
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
