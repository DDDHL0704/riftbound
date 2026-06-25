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
    public void CardBehaviorRegistryIdentifiesStackTriggerSourceUnitsByEffectKind(
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
    public void CardBehaviorRegistryRejectsNonMatchingStackTriggerSourceUnits(
        string cardNo,
        string effectKind)
    {
        Assert.False(CardBehaviorRegistry.IsImplementedUnitWithEffectKind(cardNo, effectKind));
    }

    [Fact]
    public void CoreRuleEngineTriggerSourceSelectionUsesCatalogEffectKindIdentity()
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
        Assert.Contains("IsControlledFaceUpFieldUnitWithEffectKind", source, StringComparison.Ordinal);
        Assert.Contains("CardBehaviorRegistry.IsImplementedUnitWithEffectKind", source, StringComparison.Ordinal);
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
