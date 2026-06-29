using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class PlayBehaviorSourceIdentityGuardTests
{
    [Theory]
    [InlineData("OGN·031/298", "RAGING_DRAKE_NEXT_SPELL_COST_PLAY_UNIT")]
    [InlineData("OGN·061/298", "PORO_HERDER_NO_PORO_STATIC_PLAY_UNIT")]
    [InlineData("UNL-097/219", "BALANCED_DISCIPLE_NO_OTHER_POWER_VANILLA_PLAY_UNIT")]
    [InlineData("UNL-122/219", "CRESCENT_GUARD_NO_SPELL_VANILLA_PLAY_UNIT")]
    [InlineData("UNL-004/219", "ASCENDED_BELIEVER_NO_SPELL_VANILLA_PLAY_UNIT")]
    [InlineData("UNL-108/219", "SLY_SALAMANDER_NO_EXPERIENCE_VANILLA_PLAY_UNIT")]
    [InlineData("OGN·019/298", "RAMPAGING_SOUL_NO_DISCARD_SPIRIT_PLAY_UNIT")]
    [InlineData("SFD·002/221", "ARMED_ASSAULTER_PLAY_UNIT_NO_OPTIONAL_HASTE")]
    [InlineData("SFD·109/221", "AKSHAN_NO_OPTIONAL_ASSEMBLE_NO_EXTRA_PLAY_UNIT")]
    public void CardBehaviorRegistryIdentifiesPlaySourceUnitsByEffectKind(
        string cardNo,
        string effectKind)
    {
        Assert.True(CardBehaviorRegistry.IsImplementedUnitWithEffectKind(cardNo, effectKind));
    }

    [Theory]
    [InlineData("OGN·031/298", "EAGER_APPRENTICE_SPELL_COST_STATIC_PLAY_UNIT")]
    [InlineData("OGN·084/298", "RAGING_DRAKE_NEXT_SPELL_COST_PLAY_UNIT")]
    [InlineData("OGN·061/298", "RAGING_DRAKE_NEXT_SPELL_COST_PLAY_UNIT")]
    [InlineData("OGN·031/298", "PORO_HERDER_NO_PORO_STATIC_PLAY_UNIT")]
    [InlineData("UNL-097/219", "PORO_HERDER_NO_PORO_STATIC_PLAY_UNIT")]
    [InlineData("OGN·061/298", "BALANCED_DISCIPLE_NO_OTHER_POWER_VANILLA_PLAY_UNIT")]
    [InlineData("UNL-122/219", "RAGING_DRAKE_NEXT_SPELL_COST_PLAY_UNIT")]
    [InlineData("OGN·031/298", "CRESCENT_GUARD_NO_SPELL_VANILLA_PLAY_UNIT")]
    [InlineData("UNL-108/219", "ASCENDED_BELIEVER_NO_SPELL_VANILLA_PLAY_UNIT")]
    [InlineData("OGN·019/298", "SLY_SALAMANDER_NO_EXPERIENCE_VANILLA_PLAY_UNIT")]
    [InlineData("UNL-004/219", "RAMPAGING_SOUL_NO_DISCARD_SPIRIT_PLAY_UNIT")]
    [InlineData("SFD·109/221", "ARMED_ASSAULTER_PLAY_UNIT_NO_OPTIONAL_HASTE")]
    [InlineData("SFD·002/221", "AKSHAN_NO_OPTIONAL_ASSEMBLE_NO_EXTRA_PLAY_UNIT")]
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

    [Fact]
    public void PoroHerderBoonDrawPlaySourceUsesCatalogEffectKind()
    {
        var coreRuleEngineSource = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs"));

        Assert.DoesNotContain("PoroHerderCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Equals(behavior.CardNo, PoroHerderCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("PoroHerderBoonDrawSourceEffectKind", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains(
            "string.Equals(behavior.EffectKind, PoroHerderBoonDrawSourceEffectKind",
            coreRuleEngineSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void BalancedDiscipleOtherPowerDrawPlaySourceUsesCatalogEffectKind()
    {
        var coreRuleEngineSource = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs"));

        Assert.DoesNotContain("BalancedDiscipleCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Equals(behavior.CardNo, BalancedDiscipleCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("BalancedDiscipleOtherPowerDrawSourceEffectKind", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains(
            "string.Equals(behavior.EffectKind, BalancedDiscipleOtherPowerDrawSourceEffectKind",
            coreRuleEngineSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CrescentGuardReadyOptionalCostSourceUsesBehaviorFields()
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

        Assert.DoesNotContain("CrescentGuardCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CrescentGuardCardNo", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Equals(behavior.CardNo, CrescentGuardCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Equals(behavior.CardNo, CrescentGuardCardNo", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CrescentGuardReadyOptionalCostSourceEffectKind", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CrescentGuardReadyOptionalCostSourceEffectKind", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CRESCENT_GUARD_NO_SPELL_VANILLA_PLAY_UNIT", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CRESCENT_GUARD_NO_SPELL_VANILLA_PLAY_UNIT", matchSessionSource, StringComparison.Ordinal);
        Assert.Contains("behavior.SourceReadyAdditionalPowerCost", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("behavior.SourceReadyAdditionalPowerTrait", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("behavior.SourceReadyConditionKind", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("behavior.SourceReadyAdditionalPowerCost", matchSessionSource, StringComparison.Ordinal);
        Assert.Contains("behavior.SourceReadyAdditionalPowerTrait", matchSessionSource, StringComparison.Ordinal);
        Assert.Contains("behavior.SourceReadyConditionKind", matchSessionSource, StringComparison.Ordinal);
        Assert.Contains("CardSourceReadyConditionKinds.ControllerPlayedSpellThisTurn", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("CardSourceReadyConditionKinds.ControllerPlayedSpellThisTurn", matchSessionSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ConditionalSourceUnitPowerAndTagsUseCatalogEffectKind()
    {
        var coreRuleEngineSource = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs"));

        Assert.DoesNotContain("AscendedBelieverCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SlySalamanderCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RampagingSoulCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Equals(behavior.CardNo, AscendedBelieverCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Equals(behavior.CardNo, SlySalamanderCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Equals(behavior.CardNo, RampagingSoulCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("AscendedBelieverConditionalSourceEffectKind", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("SlySalamanderConditionalSourceEffectKind", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("RampagingSoulConditionalSourceEffectKind", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains(
            "string.Equals(behavior.EffectKind, AscendedBelieverConditionalSourceEffectKind",
            coreRuleEngineSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "string.Equals(behavior.EffectKind, SlySalamanderConditionalSourceEffectKind",
            coreRuleEngineSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "string.Equals(behavior.EffectKind, RampagingSoulConditionalSourceEffectKind",
            coreRuleEngineSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void OptionalCostRepresentativeSourcesUseCatalogEffectKind()
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

        Assert.DoesNotContain("string.Equals(behavior.CardNo, ArmedAssaulterCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Equals(behavior.CardNo, AkshanCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Equals(behavior.CardNo, AkshanCardNo", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("akshanState.CardNo, AkshanCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("ArmedAssaulterHasteTemperedSourceEffectKind", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("AkshanOrangeExtraEquipmentStealSourceEffectKind", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("AkshanOrangeExtraEquipmentStealSourceEffectKind", matchSessionSource, StringComparison.Ordinal);
        Assert.Contains(
            "string.Equals(behavior.EffectKind, ArmedAssaulterHasteTemperedSourceEffectKind",
            coreRuleEngineSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "string.Equals(behavior.EffectKind, AkshanOrangeExtraEquipmentStealSourceEffectKind",
            coreRuleEngineSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "string.Equals(behavior.EffectKind, AkshanOrangeExtraEquipmentStealSourceEffectKind",
            matchSessionSource,
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
