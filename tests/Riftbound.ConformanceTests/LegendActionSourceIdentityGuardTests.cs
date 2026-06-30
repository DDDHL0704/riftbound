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
    public void CoreLegendIdentitySourceRowsUseSharedCatalog()
    {
        Assert.Equal(
            ["OGN·255/298", "OGN·303/298", "OGN·303*/298"],
            LegendIdentityCatalog.SourceCardNosForIdentity(LegendIdentityCatalog.AhriLegendIdentityId));
        Assert.Equal(
            ["SFD·183/221", "SFD·241/221"],
            LegendIdentityCatalog.SourceCardNosForIdentity(LegendIdentityCatalog.LucianLegendIdentityId));
        Assert.Equal(
            ["UNL-191/219", "UNL-231/219", "UNL-231*/219"],
            LegendIdentityCatalog.SourceCardNosForIdentity(LegendIdentityCatalog.MasterYiLevelLegendIdentityId));
        Assert.Equal(
            ["SFD·185/221", "SFD·242/221"],
            LegendIdentityCatalog.SourceCardNosForIdentity(LegendIdentityCatalog.DravenLegendIdentityId));
        Assert.Equal(
            ["OGS·023/024"],
            LegendIdentityCatalog.SourceCardNosForIdentity(LegendIdentityCatalog.GarenIntroLegendIdentityId));
        Assert.Equal(
            ["OGS·021/024"],
            LegendIdentityCatalog.SourceCardNosForIdentity(LegendIdentityCatalog.LuxIntroLegendIdentityId));
        Assert.Equal(
            ["OGS·017/024"],
            LegendIdentityCatalog.SourceCardNosForIdentity(LegendIdentityCatalog.AnnieLegendIdentityId));
        Assert.Equal(
            ["FND-251/298", "OGN·251/298", "OGN·301/298", "OGN·301*/298"],
            LegendIdentityCatalog.SourceCardNosForIdentity(LegendIdentityCatalog.JinxLegendIdentityId));
        Assert.Equal(
            ["SFD·181/221", "SFD·240/221"],
            LegendIdentityCatalog.SourceCardNosForIdentity(LegendIdentityCatalog.RumbleLegendIdentityId));
        Assert.Equal(
            ["FND-249/298", "OGN·249/298", "OGN·300/298", "OGN·300*/298", "SFD·205/221", "SFD·251/221"],
            LegendIdentityCatalog.SourceCardNosForIdentity(LegendIdentityCatalog.PowerfulUnitRuneLegendIdentityId));
        Assert.Equal(
            ["OGN·269/298", "OGN·310/298", "OGN·310*/298"],
            LegendIdentityCatalog.SourceCardNosForIdentity(LegendIdentityCatalog.SettLegendIdentityId));
        Assert.Equal(
            ["UNL-187/219", "UNL-229/219", "UNL-229*/219"],
            LegendIdentityCatalog.SourceCardNosForIdentity(LegendIdentityCatalog.ViLegendIdentityId));
        Assert.Equal(
            ["UNL-193/219", "UNL-232/219", "UNL-232*/219"],
            LegendIdentityCatalog.SourceCardNosForIdentity(LegendIdentityCatalog.VexLegendIdentityId));
        Assert.Equal(
            ["SFD·201/221", "SFD·249/221"],
            LegendIdentityCatalog.SourceCardNosForIdentity(LegendIdentityCatalog.RenataLegendIdentityId));
        Assert.Equal(
            ["SFD·187/221", "SFD·243/221"],
            LegendIdentityCatalog.SourceCardNosForIdentity(LegendIdentityCatalog.ReksaiLegendIdentityId));
        Assert.Equal(
            ["UNL-195/219", "UNL-233/219", "UNL-233*/219"],
            LegendIdentityCatalog.SourceCardNosForIdentity(LegendIdentityCatalog.IvernLegendIdentityId));
        Assert.Equal(
            ["UNL-199/219", "UNL-235/219", "UNL-235*/219"],
            LegendIdentityCatalog.SourceCardNosForIdentity(LegendIdentityCatalog.LeblancLegendIdentityId));
        Assert.Equal(
            ["UNL-183/219", "UNL-227/219", "UNL-227*/219"],
            LegendIdentityCatalog.SourceCardNosForIdentity(LegendIdentityCatalog.RengarLegendIdentityId));
        Assert.Equal(
            ["OGN·261/298", "OGN·306/298", "OGN·306*/298"],
            LegendIdentityCatalog.SourceCardNosForIdentity(LegendIdentityCatalog.LeonaLegendIdentityId));
        Assert.Equal(
            ["SFD·203/221", "SFD·250/221"],
            LegendIdentityCatalog.SourceCardNosForIdentity(LegendIdentityCatalog.SivirLegendIdentityId));
        Assert.Equal(
            ["UNL-181/219", "UNL-226/219", "UNL-226*/219"],
            LegendIdentityCatalog.SourceCardNosForIdentity(LegendIdentityCatalog.JhinLegendIdentityId));
        Assert.Equal("UNL-183/219", LegendIdentityCatalog.PrimarySourceCardNoForIdentity(LegendIdentityCatalog.RengarLegendIdentityId));
        Assert.True(LegendIdentityCatalog.IsSourceCardNoForIdentity(
            LegendIdentityCatalog.PowerfulUnitRuneLegendIdentityId,
            "SFD·251/221"));
        Assert.False(LegendIdentityCatalog.IsSourceCardNoForIdentity(
            LegendIdentityCatalog.JhinLegendIdentityId,
            "SFD·181/221"));

        var coreRuleEngineSource = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs"));

        Assert.Contains("LegendIdentityCatalog.SourceCardNosForIdentity", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[AhriLegendCardNo, \"OGN·303/298\", \"OGN·303*/298\"]", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[LucianLegendCardNo, \"SFD·241/221\"]", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[MasterYiLevelLegendCardNo, \"UNL-231/219\", \"UNL-231*/219\"]", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[DravenLegendCardNo, \"SFD·242/221\"]", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[GarenIntroLegendCardNo]", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[LuxIntroLegendCardNo]", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[AnnieIntroLegendCardNo]", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[JinxLegendCardNo, \"OGN·251/298\", \"OGN·301/298\", \"OGN·301*/298\"]", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[RumbleLegendCardNo, \"SFD·240/221\"]", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[SettLegendCardNo, \"OGN·310/298\", \"OGN·310*/298\"]", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[ViLegendCardNo, \"UNL-229/219\", \"UNL-229*/219\"]", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[VexLegendCardNo, \"UNL-232/219\", \"UNL-232*/219\"]", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[RenataLegendCardNo, \"SFD·249/221\"]", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[ReksaiLegendCardNo, \"SFD·243/221\"]", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[IvernLegendCardNo, \"UNL-233/219\", \"UNL-233*/219\"]", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[LeblancLegendCardNo, \"UNL-235/219\", \"UNL-235*/219\"]", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[RengarLegendCardNo, \"UNL-227/219\", \"UNL-227*/219\"]", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[LeonaOriginLegendCardNo, \"OGN·306/298\", \"OGN·306*/298\"]", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[SivirSpiritforgedLegendCardNo, \"SFD·250/221\"]", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[JhinLegendCardNo, \"UNL-226/219\", \"UNL-226*/219\"]", coreRuleEngineSource, StringComparison.Ordinal);
    }

    [Fact]
    public void AzirAndLilliaLegendActionSourceGroupsUseSharedCatalog()
    {
        AssertSourceCardNosEquivalent(
            ["SFD·197/221", "SFD·247/221"],
            LegendActionAbilityCatalog.SourceCardNosForAbility(LegendActionAbilityCatalog.AzirLegendAbilityId));
        AssertSourceCardNosEquivalent(
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

    [Fact]
    public void ReactionAndDynamicLegendActionSourceGroupsUseSharedCatalog()
    {
        AssertSourceCardNosEquivalent(
            ["OGN·253/298", "OGN·302/298", "OGN·302*/298"],
            LegendActionAbilityCatalog.SourceCardNosForAbility(LegendActionAbilityCatalog.DariusLegendAbilityId));
        AssertSourceCardNosEquivalent(
            ["UNL-197/219", "UNL-234/219", "UNL-234*/219"],
            LegendActionAbilityCatalog.SourceCardNosForAbility(LegendActionAbilityCatalog.DianaLegendAbilityId));
        AssertSourceCardNosEquivalent(
            ["OGN·247/298", "OGN·299/298", "OGN·299*/298"],
            LegendActionAbilityCatalog.SourceCardNosForAbility(LegendActionAbilityCatalog.KaisaLegendAbilityId));
        AssertSourceCardNosEquivalent(
            ["SFD·189/221", "SFD·244/221"],
            LegendActionAbilityCatalog.SourceCardNosForAbility(LegendActionAbilityCatalog.OrnnLegendAbilityId));
        AssertSourceCardNosEquivalent(
            ["SFD·199/221", "SFD·248/221"],
            LegendActionAbilityCatalog.SourceCardNosForAbility(LegendActionAbilityCatalog.EzrealLegendAbilityId));
        AssertSourceCardNosEquivalent(
            ["SFD·195/221", "SFD·195a/221·P", "SFD·246/221"],
            LegendActionAbilityCatalog.SourceCardNosForAbility(LegendActionAbilityCatalog.IreliaLegendAbilityId));
        AssertSourceCardNosEquivalent(
            ["OGN·263/298", "OGN·263a/298", "OGN·307/298", "OGN·307*/298"],
            LegendActionAbilityCatalog.SourceCardNosForAbility(LegendActionAbilityCatalog.TeemoLegendAbilityId));
        Assert.True(LegendActionAbilityCatalog.IsSourceCardNoForAbility(
            LegendActionAbilityCatalog.TeemoLegendAbilityId,
            "OGN·307*/298"));
        Assert.False(LegendActionAbilityCatalog.IsSourceCardNoForAbility(
            LegendActionAbilityCatalog.DianaLegendAbilityId,
            "SFD·199/221"));

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

        Assert.DoesNotContain("[DariusOriginLegendCardNo, \"OGN·302/298\", \"OGN·302*/298\"]", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[\"UNL-197/219\", \"UNL-234/219\", \"UNL-234*/219\"]", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[\"OGN·247/298\", \"OGN·299/298\", \"OGN·299*/298\"]", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[\"SFD·189/221\", \"SFD·244/221\"]", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[\"SFD·199/221\", \"SFD·248/221\"]", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[IreliaLegendCardNo, \"SFD·195a/221·P\", \"SFD·246/221\"]", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[TeemoOriginLegendCardNo, \"OGN·263a/298\", \"OGN·307/298\", \"OGN·307*/298\"]", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[DariusOriginLegendCardNo, \"OGN·302/298\", \"OGN·302*/298\"]", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[\"UNL-197/219\", \"UNL-234/219\", \"UNL-234*/219\"]", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[\"OGN·247/298\", \"OGN·299/298\", \"OGN·299*/298\"]", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[\"SFD·189/221\", \"SFD·244/221\"]", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[\"SFD·199/221\", \"SFD·248/221\"]", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[IreliaLegendCardNo, \"SFD·195a/221·P\", \"SFD·246/221\"]", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[TeemoOriginLegendCardNo, \"OGN·263a/298\", \"OGN·307/298\", \"OGN·307*/298\"]", matchSessionSource, StringComparison.Ordinal);
    }

    [Fact]
    public void MainLegendActionSourceGroupsUseSharedCatalog()
    {
        AssertSourceCardNosEquivalent(
            ["FND-259/298", "OGN·259/298", "OGN·305*/298", "OGN·305/298"],
            LegendActionAbilityCatalog.SourceCardNosForAbility(LegendActionAbilityCatalog.YasuoLegendAbilityId));
        AssertSourceCardNosEquivalent(
            ["OGN·257/298", "OGN·304*/298", "OGN·304/298"],
            LegendActionAbilityCatalog.SourceCardNosForAbility(LegendActionAbilityCatalog.LeeSinLegendAbilityId));
        AssertSourceCardNosEquivalent(
            ["UNL-203/219", "UNL-237*/219", "UNL-237/219"],
            LegendActionAbilityCatalog.SourceCardNosForAbility(LegendActionAbilityCatalog.PoppyLegendAbilityId));
        AssertSourceCardNosEquivalent(
            ["FND-265/298", "OGN·265/298", "OGN·308*/298", "OGN·308/298"],
            LegendActionAbilityCatalog.SourceCardNosForAbility(LegendActionAbilityCatalog.ViktorLegendAbilityId));
        AssertSourceCardNosEquivalent(
            ["OGN·267/298", "OGN·309/298", "OGN·309*/298"],
            LegendActionAbilityCatalog.SourceCardNosForAbility(LegendActionAbilityCatalog.MissFortuneLegendAbilityId));
        AssertSourceCardNosEquivalent(
            ["UNL-201/219", "UNL-236/219", "UNL-236*/219"],
            LegendActionAbilityCatalog.SourceCardNosForAbility(LegendActionAbilityCatalog.KhazixLegendBoonAbilityId));
        AssertSourceCardNosEquivalent(
            ["UNL-201/219", "UNL-236/219", "UNL-236*/219"],
            LegendActionAbilityCatalog.SourceCardNosForAbility(LegendActionAbilityCatalog.KhazixLegendMoveAbilityId));
        AssertSourceCardNosEquivalent(
            ["UNL-185/219", "UNL-228/219", "UNL-228*/219"],
            LegendActionAbilityCatalog.SourceCardNosForAbility(LegendActionAbilityCatalog.PykeLegendAbilityId));
        AssertSourceCardNosEquivalent(
            ["SFD·193/221", "SFD·245/221"],
            LegendActionAbilityCatalog.SourceCardNosForAbility(LegendActionAbilityCatalog.JaxLegendAttachAbilityId));
        AssertSourceCardNosEquivalent(
            ["SFD·193/221", "SFD·245/221"],
            LegendActionAbilityCatalog.SourceCardNosForAbility(LegendActionAbilityCatalog.JaxLegendReattachAbilityId));
        Assert.True(LegendActionAbilityCatalog.IsSourceCardNoForAbility(
            LegendActionAbilityCatalog.KhazixLegendMoveAbilityId,
            "UNL-236*/219"));
        Assert.False(LegendActionAbilityCatalog.IsSourceCardNoForAbility(
            LegendActionAbilityCatalog.PykeLegendAbilityId,
            "SFD·245/221"));

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

        Assert.DoesNotContain("[YasuoLegendCardNo, \"OGN·259/298\", \"OGN·305*/298\", \"OGN·305/298\"]", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[LeeSinLegendCardNo, \"OGN·304*/298\", \"OGN·304/298\"]", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[\"UNL-203/219\", \"UNL-237*/219\", PoppyLegendCardNo]", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[ViktorLegendCardNo, \"OGN·265/298\", \"OGN·308*/298\", \"OGN·308/298\"]", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[MissFortuneLegendCardNo, \"OGN·309/298\", \"OGN·309*/298\"]", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[KhazixLegendCardNo, \"UNL-236/219\", \"UNL-236*/219\"]", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[PykeLegendCardNo, \"UNL-228/219\", \"UNL-228*/219\"]", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[JaxSpiritforgedLegendCardNo, \"SFD·245/221\"]", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[YasuoLegendCardNo, \"OGN·259/298\", \"OGN·305*/298\", \"OGN·305/298\"]", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[LeeSinLegendCardNo, \"OGN·304*/298\", \"OGN·304/298\"]", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[\"UNL-203/219\", \"UNL-237*/219\", PoppyLegendCardNo]", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[ViktorLegendCardNo, \"OGN·265/298\", \"OGN·308*/298\", \"OGN·308/298\"]", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[MissFortuneLegendCardNo, \"OGN·309/298\", \"OGN·309*/298\"]", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[KhazixLegendCardNo, \"UNL-236/219\", \"UNL-236*/219\"]", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[PykeLegendCardNo, \"UNL-228/219\", \"UNL-228*/219\"]", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[JaxSpiritforgedLegendCardNo, \"SFD·245/221\"]", matchSessionSource, StringComparison.Ordinal);
    }

    [Fact]
    public void LegendActionAbilityCatalogSourceGroupsDoNotHardcodeCardNumberArrays()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "LegendActionAbilityCatalog.cs"));

        Assert.DoesNotContain(
            "IReadOnlyDictionary<string, IReadOnlyList<string>> SourceCardNosByAbilityId",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "[YasuoLegendAbilityId] = [\"FND-259/298\", \"OGN·259/298\", \"OGN·305*/298\", \"OGN·305/298\"]",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "[TeemoLegendAbilityId] = [\"OGN·263/298\", \"OGN·263a/298\", \"OGN·307/298\", \"OGN·307*/298\"]",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "OfficialCardSourceIdentityGroups",
            source,
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

    private static void AssertSourceCardNosEquivalent(
        IReadOnlyList<string> expected,
        IReadOnlyList<string> actual)
    {
        Assert.Equal(
            expected.Order(StringComparer.Ordinal),
            actual.Order(StringComparer.Ordinal));
    }
}
