using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class TriggerSourceIdentityGuardTests
{
    [Theory]
    [InlineData("OGN·059/298", "ECLIPSE_VANGUARD_STUN_TRIGGER_PLAY_UNIT")]
    [InlineData("OGN·103/298", "RAVENBLOOM_STUDENT_SPELL_TRIGGER_PLAY_UNIT")]
    [InlineData("OGS·006/024", "OGS_LUX_HIGH_COST_SPELL_TRIGGER_PLAY_UNIT")]
    [InlineData("OGN·091/298", "ARENA_SERVICE_CREW_EQUIPMENT_TRIGGER_PLAY_UNIT")]
    [InlineData("OGN·130/298", "SHARPSHOOTER_PIRATE_ATTACK_TRIGGER_PLAY_UNIT")]
    [InlineData("OGN·167/298", "EMBER_MONK_STANDBY_TRIGGER_PLAY_UNIT")]
    [InlineData("OGN·178/298", "UNDERCOVER_AGENT_LAST_BREATH_PLAY_UNIT")]
    [InlineData("OGN·190/298", "OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT")]
    [InlineData("SFD·155/221", "HONEST_BROKER_LAST_BREATH_GOLD_PLAY_UNIT")]
    [InlineData("SFD·167/221", "UNSUNG_HERO_LAST_BREATH_POWERFUL_DRAW_PLAY_UNIT")]
    public void CardBehaviorRegistryIdentifiesCatalogTriggerSourceUnitsByEffectKind(
        string cardNo,
        string effectKind)
    {
        Assert.True(CardBehaviorRegistry.IsImplementedUnitWithEffectKind(cardNo, effectKind));
    }

    [Theory]
    [InlineData("OGN·059/298", "RAVENBLOOM_STUDENT_SPELL_TRIGGER_PLAY_UNIT")]
    [InlineData("OGN·103/298", "ECLIPSE_VANGUARD_STUN_TRIGGER_PLAY_UNIT")]
    [InlineData("OGN·091/298", "OGS_LUX_HIGH_COST_SPELL_TRIGGER_PLAY_UNIT")]
    [InlineData("OGS·006/024", "ARENA_SERVICE_CREW_EQUIPMENT_TRIGGER_PLAY_UNIT")]
    [InlineData("OGN·167/298", "SHARPSHOOTER_PIRATE_ATTACK_TRIGGER_PLAY_UNIT")]
    [InlineData("OGN·130/298", "EMBER_MONK_STANDBY_TRIGGER_PLAY_UNIT")]
    [InlineData("OGN·121/298", "EMBER_MONK_STANDBY_TRIGGER_PLAY_UNIT")]
    [InlineData("OGN·167/298", "RAVENBLOOM_STUDENT_SPELL_TRIGGER_PLAY_UNIT")]
    [InlineData("OGN·178/298", "OGN_KOGMAW_LAST_BREATH_AOE_PLAY_UNIT")]
    [InlineData("OGN·190/298", "UNDERCOVER_AGENT_LAST_BREATH_PLAY_UNIT")]
    [InlineData("SFD·155/221", "UNSUNG_HERO_LAST_BREATH_POWERFUL_DRAW_PLAY_UNIT")]
    [InlineData("SFD·167/221", "HONEST_BROKER_LAST_BREATH_GOLD_PLAY_UNIT")]
    public void CardBehaviorRegistryRejectsNonMatchingCatalogTriggerSourceUnits(
        string cardNo,
        string effectKind)
    {
        Assert.False(CardBehaviorRegistry.IsImplementedUnitWithEffectKind(cardNo, effectKind));
    }

    [Fact]
    public void CoreRuleEngineTriggerSourceSelectionUsesBehaviorFieldsWhereAvailable()
    {
        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var source = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain("string.Equals(sourceState.CardNo, EclipseVanguardCardNo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Equals(sourceState.CardNo, RavenbloomStudentCardNo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Equals(sourceState.CardNo, OgsLuxHighCostSpellCardNo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Equals(sourceState.CardNo, ArenaServiceCrewCardNo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Equals(sourceState.CardNo, EmberMonkCardNo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Equals(attackerState.CardNo, SharpshooterPirateCardNo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private const string EclipseVanguardCardNo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private const string ArenaServiceCrewCardNo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private static readonly CardBehaviorDefinition EclipseVanguardStunTriggerBehavior", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private const string EmberMonkCardNo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private const string SharpshooterPirateCardNo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ArenaServiceCrewEquipmentTriggerSourceEffectKind", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ARENA_SERVICE_CREW_EQUIPMENT_TRIGGER_PLAY_UNIT", source, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Equals(destroyedState.CardNo, KogmawCardNo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Equals(destroyedState.CardNo, UndercoverAgentCardNo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Equals(destroyedState.CardNo, HonestBrokerCardNo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Equals(destroyedState.CardNo, UnsungHeroCardNo", source, StringComparison.Ordinal);
        Assert.Contains("IsControlledFaceUpFieldUnitWithEffectKind", source, StringComparison.Ordinal);
        Assert.Contains("CardBehaviorRegistry.IsImplementedUnitWithEffectKind", source, StringComparison.Ordinal);
        Assert.Contains("CardBehaviorRegistry.TryGetByEffectKind(EclipseVanguardStunTriggerSourceEffectKind", source, StringComparison.Ordinal);
        Assert.Contains("SourceReadiesWhenControllerPlaysEquipment", source, StringComparison.Ordinal);
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
