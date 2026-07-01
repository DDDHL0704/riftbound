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

    [Fact]
    public void SourceObjectPowerStaticAuraExecutionRoutesThroughBehaviorSpecScope()
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

        Assert.DoesNotContain("StaticAuraSpecRules.TryGetSourceObjectPowerAura", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetSourceObjectFilteredPowerAura", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetSourceObjectPowerAura", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetSourceObjectFilteredPowerAura", matchSessionSource, StringComparison.Ordinal);
        Assert.Contains("ResolveSourceObjectPowerStaticAuraBonus", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("BuildSourceObjectPowerStaticAuraEffects", matchSessionSource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsSourceObjectPowerStaticAura", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsSourceObjectPowerStaticAura", matchSessionSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceObjectKeywordStaticAuraExecutionRoutesThroughBehaviorSpecScope()
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

        Assert.DoesNotContain("StaticAuraSpecRules.TryGetSourceObjectFilteredKeywordAura", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetSourceObjectFilteredKeywordAura", matchSessionSource, StringComparison.Ordinal);
        Assert.Contains("ResolveSourceObjectKeywordStaticAuraBonus", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("BuildSourceObjectKeywordStaticAuraEffects", matchSessionSource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsSourceObjectKeywordStaticAura", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsSourceObjectKeywordStaticAura", matchSessionSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceBattleStatePowerStaticAuraExecutionRoutesThroughBehaviorSpecScope()
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

        Assert.DoesNotContain("StaticAuraSpecRules.TryGetSourceAttackingWithAnotherUnitPowerAura", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetSourceLoneBattlePowerAura", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetSourceAttackingReadyEnemyUnitPowerAura", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetSourceAttackingWithAnotherUnitPowerAura", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetSourceLoneBattlePowerAura", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetSourceAttackingReadyEnemyUnitPowerAura", matchSessionSource, StringComparison.Ordinal);
        Assert.Contains("ResolveSourceBattleStatePowerStaticAuraBonus", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("BuildSourceBattleStatePowerStaticAuraEffects", matchSessionSource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsSourceBattleStatePowerStaticAura", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsSourceBattleStatePowerStaticAura", matchSessionSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceParticipantCountPowerStaticAuraExecutionRoutesThroughBehaviorSpecScope()
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

        Assert.DoesNotContain("StaticAuraSpecRules.TryGetFriendlyEquipmentPowerAura", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetSameBattlefieldFriendlyFilteredUnitCountToSourcePowerAura", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetSourceSameLocationOtherFriendlyUnitPowerAura", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetFriendlyEquipmentPowerAura", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetSameBattlefieldFriendlyFilteredUnitCountToSourcePowerAura", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetSourceSameLocationOtherFriendlyUnitPowerAura", matchSessionSource, StringComparison.Ordinal);
        Assert.Contains("ResolveSourceParticipantCountPowerStaticAuraBonus", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("BuildSourceParticipantCountPowerStaticAuraEffects", matchSessionSource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsSourceParticipantCountPowerStaticAura", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsSourceParticipantCountPowerStaticAura", matchSessionSource, StringComparison.Ordinal);
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
