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

    [Fact]
    public void BattlefieldKeywordStaticAuraExecutionRoutesThroughBehaviorSpecScope()
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

        Assert.DoesNotContain("StaticAuraSpecRules.TryGetBattlefieldAllUnitsKeywordAura", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetBattlefieldFilteredUnitsKeywordAura", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetBattlefieldAllUnitsKeywordAura", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetBattlefieldFilteredUnitsKeywordAura", matchSessionSource, StringComparison.Ordinal);
        Assert.Contains("ResolveBattlefieldKeywordStaticAuraBonus", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("BuildBattlefieldKeywordAuraEffects", matchSessionSource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsBattlefieldKeywordStaticAura", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsBattlefieldKeywordStaticAura", matchSessionSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SameBattlefieldOtherFriendlyPowerStaticAuraExecutionRoutesThroughBehaviorSpecScope()
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

        Assert.DoesNotContain("StaticAuraSpecRules.TryGetSameBattlefieldOtherFriendlyUnitsPowerAura", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetSameBattlefieldOtherFriendlyFilteredUnitsPowerAura", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetSameBattlefieldOtherFriendlyUnitsPowerAura", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetSameBattlefieldOtherFriendlyFilteredUnitsPowerAura", matchSessionSource, StringComparison.Ordinal);
        Assert.Contains("ResolveSameBattlefieldOtherFriendlyPowerStaticAuraBonus", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("BuildSameBattlefieldOtherFriendlyPowerStaticAuraEffects", matchSessionSource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsSameBattlefieldOtherFriendlyPowerStaticAura", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsSameBattlefieldOtherFriendlyPowerStaticAura", matchSessionSource, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicFieldFriendlyPowerStaticAuraExecutionRoutesThroughBehaviorSpecScope()
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

        Assert.DoesNotContain("StaticAuraSpecRules.TryGetOtherFriendlyUnitsPowerAura", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetFriendlyFilteredUnitsPowerAura", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetOtherFriendlyUnitsPowerAura", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetFriendlyFilteredUnitsPowerAura", matchSessionSource, StringComparison.Ordinal);
        Assert.Contains("ResolvePublicFieldFriendlyPowerStaticAuraBonus", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("BuildPublicFieldFriendlyPowerStaticAuraEffects", matchSessionSource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsPublicFieldFriendlyPowerStaticAura", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsPublicFieldFriendlyPowerStaticAura", matchSessionSource, StringComparison.Ordinal);
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
