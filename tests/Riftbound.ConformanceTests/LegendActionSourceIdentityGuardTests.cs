using Riftbound.Engine;
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

    [Fact]
    public void CoreLegendStaticIdentitySourceDoesNotUseDuplicatedCardNumberHelpers()
    {
        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain("IsAhriLegendCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsLucianLegendCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsMasterYiLevelLegendCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsDravenLegendCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("AhriLegendIdentityId", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("LucianLegendIdentityId", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("MasterYiLevelLegendIdentityId", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("DravenLegendIdentityId", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("LegendCardHasIdentity", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("TryGetLegendIdentity", coreRuleEngineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void CoreActiveLegendIdentitySourceDoesNotUseDuplicatedCardNumberHelpers()
    {
        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain("IsSettLegendCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsViLegendCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsVexLegendCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsRenataLegendCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsReksaiLegendCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsIvernLegendCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsLeblancLegendCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("SettLegendIdentityId", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("ViLegendIdentityId", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("VexLegendIdentityId", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("RenataLegendIdentityId", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("ReksaiLegendIdentityId", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("IvernLegendIdentityId", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("LeblancLegendIdentityId", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("LegendCardHasIdentity", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("TryGetLegendIdentity", coreRuleEngineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void CoreRemainingLegendIdentitySourceDoesNotUseDuplicatedCardNumberHelpers()
    {
        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain("IsRumbleLegendCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsJinxLegendCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsPowerfulUnitRuneLegendCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("RumbleLegendIdentityId", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("JinxLegendIdentityId", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("PowerfulUnitRuneLegendIdentityId", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("LegendCardHasIdentity", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("TryGetLegendIdentity", coreRuleEngineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void CoreAnnieTurnEndRuneReadySourceUsesLegendIdentity()
    {
        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain(
            "string.Equals(legendState.CardNo, AnnieIntroLegendCardNo",
            coreRuleEngineSource,
            StringComparison.Ordinal);
        Assert.Contains("AnnieLegendIdentityId", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("LegendCardHasIdentity", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("TryGetLegendIdentity", coreRuleEngineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void CoreIntroLegendSourcesUseLegendIdentity()
    {
        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain(
            "string.Equals(legendState.CardNo, GarenIntroLegendCardNo",
            coreRuleEngineSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "string.Equals(legendState.CardNo, LuxIntroLegendCardNo",
            coreRuleEngineSource,
            StringComparison.Ordinal);
        Assert.Contains("GarenIntroLegendIdentityId", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("LuxIntroLegendIdentityId", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("LegendCardHasIdentity", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("TryGetLegendIdentity", coreRuleEngineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void AzirAndLilliaLegendActionSourceGroupsUseSharedCatalog()
    {
        Assert.Equal(
            ["SFD·197/221", "SFD·247/221"],
            LegendActionAbilityCatalog.SourceCardNosForAbility(LegendActionAbilityCatalog.AzirLegendAbilityId));
        Assert.Equal(
            ["UNL-189/219", "UNL-230/219", "UNL-230*/219"],
            LegendActionAbilityCatalog.SourceCardNosForAbility(LegendActionAbilityCatalog.LilliaLegendAbilityId));
        Assert.True(LegendActionAbilityCatalog.IsSourceCardNoForAbility(
            LegendActionAbilityCatalog.AzirLegendAbilityId,
            "SFD·197/221"));
        Assert.True(LegendActionAbilityCatalog.IsSourceCardNoForAbility(
            LegendActionAbilityCatalog.LilliaLegendAbilityId,
            "UNL-230*/219"));
        Assert.False(LegendActionAbilityCatalog.IsSourceCardNoForAbility(
            LegendActionAbilityCatalog.AzirLegendAbilityId,
            "UNL-189/219"));

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

        Assert.DoesNotContain("private const string AzirSpiritforgedLegendCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private const string LilliaLegendCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[AzirSpiritforgedLegendCardNo, \"SFD·247/221\"]", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[LilliaLegendCardNo, \"UNL-230/219\", \"UNL-230*/219\"]", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private const string AzirSpiritforgedLegendCardNo", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private const string LilliaLegendCardNo", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[AzirSpiritforgedLegendCardNo, \"SFD·247/221\"]", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[LilliaLegendCardNo, \"UNL-230/219\", \"UNL-230*/219\"]", matchSessionSource, StringComparison.Ordinal);
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
