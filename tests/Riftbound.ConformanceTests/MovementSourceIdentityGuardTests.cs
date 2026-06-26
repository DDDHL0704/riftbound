using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class MovementSourceIdentityGuardTests
{
    [Theory]
    [InlineData("OGN·125/298", "BILGEWATER_BULLY_NO_BOON_ROAM_PLAY_UNIT")]
    public void CardBehaviorRegistryIdentifiesMovementSourceUnitsByEffectKind(
        string cardNo,
        string effectKind)
    {
        Assert.True(CardBehaviorRegistry.IsImplementedUnitWithEffectKind(cardNo, effectKind));
    }

    [Theory]
    [InlineData("OGN·130/298", "BILGEWATER_BULLY_NO_BOON_ROAM_PLAY_UNIT")]
    [InlineData("OGN·125/298", "SHARPSHOOTER_PIRATE_ATTACK_TRIGGER_PLAY_UNIT")]
    public void CardBehaviorRegistryRejectsNonMatchingMovementSourceUnits(
        string cardNo,
        string effectKind)
    {
        Assert.False(CardBehaviorRegistry.IsImplementedUnitWithEffectKind(cardNo, effectKind));
    }

    [Fact]
    public void BilgewaterBullyBoonRoamSourceIdentityUsesCatalogEffectKind()
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

        Assert.DoesNotContain("BilgewaterBullyCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("BilgewaterBullyCardNo", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Equals(sourceState.CardNo, BilgewaterBullyCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Equals(sourceState.CardNo, BilgewaterBullyCardNo", matchSessionSource, StringComparison.Ordinal);
        Assert.Contains("CardBehaviorRegistry.IsImplementedUnitWithEffectKind", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("CardBehaviorRegistry.IsImplementedUnitWithEffectKind", matchSessionSource, StringComparison.Ordinal);
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
