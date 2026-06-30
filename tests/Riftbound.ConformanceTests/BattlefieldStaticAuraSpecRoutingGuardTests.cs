using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class BattlefieldStaticAuraSpecRoutingGuardTests
{
    [Fact]
    public void BattlefieldPowerStaticAuraExecutionRoutesThroughBehaviorSpecScope()
    {
        var root = RepositoryRoot();
        var coreRuleEngineSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs"));
        var matchSessionSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Riftbound.Engine",
            "MatchSession.cs"));

        Assert.DoesNotContain("StaticAuraSpecRules.TryGetBattlefieldAllUnitsPowerAura", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetBattlefieldFilteredUnitsPowerAura", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetBattlefieldAllUnitsPowerAura", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetBattlefieldFilteredUnitsPowerAura", matchSessionSource, StringComparison.Ordinal);
        Assert.Contains("ResolveBattlefieldPowerStaticAuraBonus", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("BuildBattlefieldPowerStaticAuraEffects", matchSessionSource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.GetStaticAuras(battlefieldState.CardNo)", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.GetStaticAuras(battlefield.CardNo)", matchSessionSource, StringComparison.Ordinal);
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
