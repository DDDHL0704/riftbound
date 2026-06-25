using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class EquipmentKeywordRepresentativeBoundaryGuardTests
{
    [Fact]
    public void EquipmentKeywordRepresentativeBoundariesUseSourceRowsNotCardNumberHelpers()
    {
        var rulesPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CardEquipmentKeywordRules.cs");
        var rulesSource = File.ReadAllText(rulesPath);

        Assert.DoesNotContain("IsAgileDirectPlayAttachRepresentativeCardNo", rulesSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsTemperedOptionalAttachRepresentativeCardNo", rulesSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsFriendlyEquipmentStaticPowerRepresentativeCardNo", rulesSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsEquipmentStateRepresentativeCardNo", rulesSource, StringComparison.Ordinal);
        Assert.Contains("EquipmentRepresentativeBoundaryKinds.AgileDirectPlayAttach", rulesSource, StringComparison.Ordinal);
        Assert.Contains("HasRepresentativeBoundary", rulesSource, StringComparison.Ordinal);
        Assert.Contains("TryGetEquipmentStateRepresentative", rulesSource, StringComparison.Ordinal);

        Assert.True(CardEquipmentKeywordRules.HasAgileDirectPlayAttachRepresentativeBoundary("SFD·022/221"));
        Assert.False(CardEquipmentKeywordRules.HasAgileDirectPlayAttachRepresentativeBoundary("SFD·033/221"));
        Assert.True(CardEquipmentKeywordRules.HasTemperedOptionalAttachRepresentativeBoundary("SFD·008/221"));
        Assert.False(CardEquipmentKeywordRules.HasTemperedOptionalAttachRepresentativeBoundary("SFD·033/221"));
        Assert.True(CardEquipmentKeywordRules.HasFriendlyEquipmentStaticPowerRepresentativeBoundary("SFD·085/221"));
        Assert.False(CardEquipmentKeywordRules.HasFriendlyEquipmentStaticPowerRepresentativeBoundary("SFD·033/221"));
        Assert.True(CardEquipmentKeywordRules.TryGetEquipmentStateRepresentative("SFD·022/221", out var stateRepresentative));
        Assert.Equal("Long Sword", stateRepresentative.CardName);
        Assert.False(CardEquipmentKeywordRules.TryGetEquipmentStateRepresentative("SFD·033/221", out _));
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
