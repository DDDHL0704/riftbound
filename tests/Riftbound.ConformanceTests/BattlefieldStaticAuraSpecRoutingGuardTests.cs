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
    public void BattlefieldPowerStaticAuraRecoveryRoutesThroughBehaviorSpecScope()
    {
        var root = RepositoryRoot();
        var matchRecoverySource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Riftbound.Engine",
            "MatchRecovery.cs"));
        var staticAuraSpecRulesSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Riftbound.Engine",
            "StaticAuraSpecRules.cs"));

        Assert.DoesNotContain("StaticAuraSpecRules.TryGetBattlefieldAllUnitsPowerAura", matchRecoverySource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetBattlefieldFilteredUnitsPowerAura", matchRecoverySource, StringComparison.Ordinal);
        Assert.DoesNotContain("TryGetBattlefieldAllUnitsPowerAura", staticAuraSpecRulesSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TryGetBattlefieldFilteredUnitsPowerAura", staticAuraSpecRulesSource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsBattlefieldAllUnitsPowerStaticAura", matchRecoverySource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsBattlefieldFilteredUnitsPowerStaticAura", matchRecoverySource, StringComparison.Ordinal);
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
        var staticAuraSpecRulesSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Riftbound.Engine",
            "StaticAuraSpecRules.cs"));

        Assert.DoesNotContain("StaticAuraSpecRules.TryGetBattlefieldAllUnitsKeywordAura", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetBattlefieldFilteredUnitsKeywordAura", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetBattlefieldAllUnitsKeywordAura", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetBattlefieldFilteredUnitsKeywordAura", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TryGetBattlefieldAllUnitsKeywordAura", staticAuraSpecRulesSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TryGetBattlefieldFilteredUnitsKeywordAura", staticAuraSpecRulesSource, StringComparison.Ordinal);
        Assert.Contains("ResolveBattlefieldKeywordStaticAuraBonus", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("BuildBattlefieldKeywordAuraEffects", matchSessionSource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsBattlefieldKeywordStaticAura", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsBattlefieldKeywordStaticAura", matchSessionSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BattlefieldAllUnitsGrantedKeywordQueriesRouteThroughBehaviorSpecScope()
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
        var staticAuraSpecRulesSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Riftbound.Engine",
            "StaticAuraSpecRules.cs"));

        Assert.DoesNotContain("StaticAuraSpecRules.TryGetBattlefieldAllUnitsGrantedKeywordAura", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetBattlefieldAllUnitsGrantedKeywordAura", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TryGetBattlefieldAllUnitsGrantedKeywordAura", staticAuraSpecRulesSource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsBattlefieldKeywordStaticAura", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsBattlefieldKeywordStaticAura", matchSessionSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BattlefieldIsolatedDefenderKeywordModifierExecutionRoutesThroughBehaviorSpecScope()
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
        var staticAuraSpecRulesSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Riftbound.Engine",
            "StaticAuraSpecRules.cs"));

        Assert.DoesNotContain("StaticAuraSpecRules.TryGetBattlefieldIsolatedDefenderKeywordModifierAura", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetBattlefieldIsolatedDefenderKeywordModifierAura", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TryGetBattlefieldIsolatedDefenderKeywordModifierAura", staticAuraSpecRulesSource, StringComparison.Ordinal);
        Assert.Contains("ResolveBattlefieldIsolatedDefenderKeywordModifier", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("BuildBattlefieldIsolatedDefenderKeywordModifierAuraEffects", matchSessionSource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsBattlefieldIsolatedDefenderKeywordModifierStaticAura", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsBattlefieldIsolatedDefenderKeywordModifierStaticAura", matchSessionSource, StringComparison.Ordinal);
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
    public void SameBattlefieldOtherFriendlyPowerStaticAuraRecoveryRoutesThroughBehaviorSpecScope()
    {
        var root = RepositoryRoot();
        var matchRecoverySource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Riftbound.Engine",
            "MatchRecovery.cs"));
        var staticAuraSpecRulesSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Riftbound.Engine",
            "StaticAuraSpecRules.cs"));

        Assert.DoesNotContain("StaticAuraSpecRules.TryGetSameBattlefieldOtherFriendlyUnitsPowerAura", matchRecoverySource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetSameBattlefieldOtherFriendlyFilteredUnitsPowerAura", matchRecoverySource, StringComparison.Ordinal);
        Assert.DoesNotContain("TryGetSameBattlefieldOtherFriendlyUnitsPowerAura", staticAuraSpecRulesSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TryGetSameBattlefieldOtherFriendlyFilteredUnitsPowerAura", staticAuraSpecRulesSource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsSameBattlefieldOtherFriendlyUnitsPowerStaticAura", matchRecoverySource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsSameBattlefieldOtherFriendlyFilteredUnitsPowerStaticAura", matchRecoverySource, StringComparison.Ordinal);
    }

    [Fact]
    public void SameBattlefieldOtherFriendlyKeywordStaticAuraExecutionRoutesThroughBehaviorSpecScope()
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
        var staticAuraSpecRulesSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Riftbound.Engine",
            "StaticAuraSpecRules.cs"));

        Assert.DoesNotContain("StaticAuraSpecRules.TryGetSameBattlefieldOtherFriendlyUnitsKeywordAura", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetSameBattlefieldOtherFriendlyUnitsKeywordAura", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TryGetSameBattlefieldOtherFriendlyUnitsKeywordAura", staticAuraSpecRulesSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraKinds.SameBattlefieldOtherFriendlyUnitsKeyword", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraKinds.SameBattlefieldOtherFriendlyUnitsKeyword", matchSessionSource, StringComparison.Ordinal);
        Assert.Contains("ResolveSameBattlefieldOtherFriendlyUnitsKeywordBonus", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("BuildSameBattlefieldOtherFriendlyUnitsKeywordAuraEffects", matchSessionSource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsSameBattlefieldOtherFriendlyKeywordStaticAura", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsSameBattlefieldOtherFriendlyKeywordStaticAura", matchSessionSource, StringComparison.Ordinal);
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
    public void PublicFieldFriendlyPowerStaticAuraRecoveryRoutesThroughBehaviorSpecScope()
    {
        var root = RepositoryRoot();
        var matchRecoverySource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Riftbound.Engine",
            "MatchRecovery.cs"));
        var staticAuraSpecRulesSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Riftbound.Engine",
            "StaticAuraSpecRules.cs"));

        Assert.DoesNotContain("StaticAuraSpecRules.TryGetFriendlyUnitsPowerAura", matchRecoverySource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetOtherFriendlyUnitsPowerAura", matchRecoverySource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetFriendlyFilteredUnitsPowerAura", matchRecoverySource, StringComparison.Ordinal);
        Assert.DoesNotContain("TryGetFriendlyUnitsPowerAura", staticAuraSpecRulesSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TryGetOtherFriendlyUnitsPowerAura", staticAuraSpecRulesSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TryGetFriendlyFilteredUnitsPowerAura", staticAuraSpecRulesSource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsFriendlyUnitsPowerStaticAura", matchRecoverySource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsOtherFriendlyUnitsPowerStaticAura", matchRecoverySource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsFriendlyFilteredUnitsPowerStaticAura", matchRecoverySource, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicFieldFriendlyKeywordStaticAuraExecutionRoutesThroughBehaviorSpecScope()
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
        var staticAuraSpecRulesSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Riftbound.Engine",
            "StaticAuraSpecRules.cs"));

        Assert.DoesNotContain("StaticAuraKinds.FriendlyFilteredUnitsKeyword", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraKinds.OtherFriendlyUnitsKeyword", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraKinds.FriendlyFilteredUnitsKeyword", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraKinds.OtherFriendlyUnitsKeyword", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TryGetFriendlyFilteredUnitsKeywordAura", staticAuraSpecRulesSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TryGetOtherFriendlyUnitsKeywordAura", staticAuraSpecRulesSource, StringComparison.Ordinal);
        Assert.Contains("ResolveFriendlyFilteredUnitsKeywordBonus", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("BuildFriendlyFilteredUnitsKeywordAuraEffects", matchSessionSource, StringComparison.Ordinal);
        Assert.Contains("BuildOtherFriendlyUnitsKeywordAuraEffects", matchSessionSource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsPublicFieldFriendlyKeywordStaticAura", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsPublicFieldFriendlyKeywordStaticAura", matchSessionSource, StringComparison.Ordinal);
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
    public void SourceObjectPowerStaticAuraRecoveryRoutesThroughBehaviorSpecScope()
    {
        var root = RepositoryRoot();
        var matchRecoverySource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Riftbound.Engine",
            "MatchRecovery.cs"));
        var staticAuraSpecRulesSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Riftbound.Engine",
            "StaticAuraSpecRules.cs"));

        Assert.DoesNotContain("StaticAuraSpecRules.TryGetSourceObjectPowerAura", matchRecoverySource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetSourceObjectFilteredPowerAura", matchRecoverySource, StringComparison.Ordinal);
        Assert.DoesNotContain("TryGetSourceObjectPowerAura", staticAuraSpecRulesSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TryGetSourceObjectFilteredPowerAura", staticAuraSpecRulesSource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsSourceObjectUnfilteredPowerStaticAura", matchRecoverySource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsSourceObjectFilteredPowerStaticAura", matchRecoverySource, StringComparison.Ordinal);
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
    public void SourceBattleStatePowerStaticAuraRecoveryRoutesThroughBehaviorSpecScope()
    {
        var root = RepositoryRoot();
        var matchRecoverySource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Riftbound.Engine",
            "MatchRecovery.cs"));
        var staticAuraSpecRulesSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Riftbound.Engine",
            "StaticAuraSpecRules.cs"));

        Assert.DoesNotContain("StaticAuraSpecRules.TryGetSourceAttackingWithAnotherUnitPowerAura", matchRecoverySource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetSourceLoneBattlePowerAura", matchRecoverySource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetSourceAttackingReadyEnemyUnitPowerAura", matchRecoverySource, StringComparison.Ordinal);
        Assert.DoesNotContain("TryGetSourceAttackingWithAnotherUnitPowerAura", staticAuraSpecRulesSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TryGetSourceLoneBattlePowerAura", staticAuraSpecRulesSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TryGetSourceAttackingReadyEnemyUnitPowerAura", staticAuraSpecRulesSource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsSourceAttackingWithAnotherUnitPowerStaticAura", matchRecoverySource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsSourceLoneBattlePowerStaticAura", matchRecoverySource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsSourceAttackingReadyEnemyUnitPowerStaticAura", matchRecoverySource, StringComparison.Ordinal);
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

    [Fact]
    public void SourceParticipantCountPowerStaticAuraRecoveryRoutesThroughBehaviorSpecScope()
    {
        var root = RepositoryRoot();
        var matchRecoverySource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Riftbound.Engine",
            "MatchRecovery.cs"));
        var staticAuraSpecRulesSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Riftbound.Engine",
            "StaticAuraSpecRules.cs"));

        Assert.DoesNotContain("StaticAuraSpecRules.TryGetFriendlyEquipmentPowerAura", matchRecoverySource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetSameBattlefieldFriendlyFilteredUnitCountToSourcePowerAura", matchRecoverySource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraSpecRules.TryGetSourceSameLocationOtherFriendlyUnitPowerAura", matchRecoverySource, StringComparison.Ordinal);
        Assert.DoesNotContain("TryGetFriendlyEquipmentPowerAura", staticAuraSpecRulesSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TryGetSameBattlefieldFriendlyFilteredUnitCountToSourcePowerAura", staticAuraSpecRulesSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TryGetSourceSameLocationOtherFriendlyUnitPowerAura", staticAuraSpecRulesSource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsFriendlyEquipmentCountToSourcePowerStaticAura", matchRecoverySource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsSameBattlefieldFriendlyFilteredUnitCountToSourcePowerStaticAura", matchRecoverySource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsSameLocationOtherFriendlyUnitPowerStaticAura", matchRecoverySource, StringComparison.Ordinal);
    }

    [Fact]
    public void FriendlySingleDefendingPowerStaticAuraExecutionRoutesThroughBehaviorSpecScope()
    {
        var root = RepositoryRoot();
        var coreRuleEngineSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs"));
        var staticAuraSpecRulesSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Riftbound.Engine",
            "StaticAuraSpecRules.cs"));

        Assert.DoesNotContain("StaticAuraSpecRules.TryGetFriendlySingleDefendingUnitPowerAura", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TryGetFriendlySingleDefendingUnitPowerAura", staticAuraSpecRulesSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticAuraKinds.FriendlySingleDefendingUnitPower))", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("ResolveFriendlySingleDefendingUnitPowerBonus", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("StaticAuraSpecRules.IsFriendlySingleDefendingUnitPowerStaticAura", coreRuleEngineSource, StringComparison.Ordinal);
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
