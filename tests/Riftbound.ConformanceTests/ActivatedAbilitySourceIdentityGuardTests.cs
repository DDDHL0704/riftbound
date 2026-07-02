using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ActivatedAbilitySourceIdentityGuardTests
{
    [Fact]
    public void CoreActivatedAbilitySourceChecksUseCatalogSourceCardGroups()
    {
        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain(
            "string.Equals(sourceState.CardNo, ability.SourceCardNo",
            coreRuleEngineSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(ability, sourceState.CardNo)",
            coreRuleEngineSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GatekeeperMaduliTargetLegalityUsesCatalogSourceCardGroup()
    {
        var repositoryRoot = RepositoryRoot();
        var coreRuleEngineSource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs"));
        var matchSessionSource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Riftbound.Engine",
            "MatchSession.cs"));

        Assert.DoesNotContain(
            "sourceState.CardNo, P4ActivatedAbilityCatalog.GatekeeperMaduliCardNo",
            coreRuleEngineSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "sourceState.CardNo, P4ActivatedAbilityCatalog.GatekeeperMaduliCardNo",
            matchSessionSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(ability, sourceState.CardNo)",
            coreRuleEngineSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(ability, sourceState.CardNo)",
            matchSessionSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void P4ActivatedAbilitySourceCardGroupsDoNotUseAbilityIdSwitches()
    {
        var catalogSource = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "P4ActivatedAbilityCatalog.cs"));

        Assert.DoesNotContain(
            "string.Equals(definition.AbilityId",
            catalogSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "OfficialCardSourceIdentityGroups",
            catalogSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void P4SigilTypedResourceProfilesAreDerivedFromBehaviorSpecs()
    {
        var catalogSource = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "P4ActivatedAbilityCatalog.cs"));

        Assert.DoesNotContain(
            "private static readonly P4SigilTypedResourceProfile[] SigilTypedResourceProfiles",
            catalogSource,
            StringComparison.Ordinal);
        Assert.Contains("BuildSigilTypedResourceProfiles", catalogSource, StringComparison.Ordinal);
        Assert.Contains("BehaviorSpecCatalogBuilder.Build", catalogSource, StringComparison.Ordinal);
        Assert.Contains("ActivatedAbilityKinds.TypedResourceSkill", catalogSource, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(
        P4ActivatedAbilityCatalog.ViDoublePowerAbilityId,
        P4ActivatedAbilityCatalog.ViCardNo,
        P4ActivatedAbilityCatalog.ViAltACardNo)]
    [InlineData(
        P4ActivatedAbilityCatalog.RenataGlascDrawAbilityId,
        P4ActivatedAbilityCatalog.RenataGlascCardNo,
        P4ActivatedAbilityCatalog.RenataGlascAltCardNo)]
    [InlineData(
        P4ActivatedAbilityCatalog.RenataGlascScoreAbilityId,
        P4ActivatedAbilityCatalog.RenataGlascCardNo,
        P4ActivatedAbilityCatalog.RenataGlascAltCardNo)]
    [InlineData(
        P4ActivatedAbilityCatalog.AzirSwiftSwapAbilityId,
        P4ActivatedAbilityCatalog.AzirCardNo,
        P4ActivatedAbilityCatalog.AzirAltCardNo)]
    [InlineData(
        P4ActivatedAbilityCatalog.EzrealBlueSwiftMoveAbilityId,
        P4ActivatedAbilityCatalog.EzrealBlueSwiftCardNo,
        P4ActivatedAbilityCatalog.EzrealBlueSwiftAltCardNo,
        P4ActivatedAbilityCatalog.EzrealBlueSwiftPromoCardNo)]
    [InlineData(
        P4ActivatedAbilityCatalog.JhinMoveResourceAbilityId,
        P4ActivatedAbilityCatalog.JhinCardNo,
        P4ActivatedAbilityCatalog.JhinAltACardNo)]
    [InlineData(
        P4ActivatedAbilityCatalog.BlueSentinelResourceAbilityId,
        P4ActivatedAbilityCatalog.BlueSentinelCardNo,
        P4ActivatedAbilityCatalog.BlueSentinelAltACardNo)]
    public void P4ActivatedAbilitySourceCardGroupsPreserveOfficialEquivalentRows(
        string abilityId,
        params string[] expectedCardNos)
    {
        Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(abilityId, out var ability));

        Assert.Equal(
            expectedCardNos.Order(StringComparer.Ordinal),
            P4ActivatedAbilityCatalog.SourceCardNosForAbility(ability).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void P4ActivatedAbilitySourceCardGroupsDoNotMergeDistinctRuntimeDefinitionsFromSameOfficialUnit()
    {
        Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(
            P4ActivatedAbilityCatalog.RageSigilResourceAbilityId,
            out var sfdRageSigil));
        Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(
            P4ActivatedAbilityCatalog.OgnRageSigilResourceAbilityId,
            out var ognRageSigil));
        Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(
            P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId,
            out var unlGold));
        Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(
            P4ActivatedAbilityCatalog.GoldTokenSfdResourceAbilityId,
            out var sfdGold));

        Assert.DoesNotContain(
            P4ActivatedAbilityCatalog.OgnRageSigilCardNo,
            P4ActivatedAbilityCatalog.SourceCardNosForAbility(sfdRageSigil));
        Assert.DoesNotContain(
            P4ActivatedAbilityCatalog.RageSigilCardNo,
            P4ActivatedAbilityCatalog.SourceCardNosForAbility(ognRageSigil));
        Assert.DoesNotContain(
            P4ActivatedAbilityCatalog.GoldTokenSfdCardNo,
            P4ActivatedAbilityCatalog.SourceCardNosForAbility(unlGold));
        Assert.DoesNotContain(
            P4ActivatedAbilityCatalog.GoldTokenUnlCardNo,
            P4ActivatedAbilityCatalog.SourceCardNosForAbility(sfdGold));
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
