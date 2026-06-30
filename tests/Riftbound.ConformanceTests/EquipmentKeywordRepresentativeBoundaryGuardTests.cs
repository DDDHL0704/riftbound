using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class EquipmentKeywordRepresentativeBoundaryGuardTests
{
    [Fact]
    public void EquipmentKeywordRepresentativeBoundariesUseBehaviorSpecDerivedRowsNotCardNumberSourceRows()
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
        Assert.Contains("BehaviorSpecCatalogBuilder.Build", rulesSource, StringComparison.Ordinal);
        foreach (var row in new[]
        {
            "new(\"SFD·022/221\", EquipmentRepresentativeBoundaryKinds.AgileDirectPlayAttach)",
            "new(\"SFD·056/221\", EquipmentRepresentativeBoundaryKinds.AgileDirectPlayAttach)",
            "new(\"SFD·064/221\", EquipmentRepresentativeBoundaryKinds.AgileDirectPlayAttach)",
            "new(\"SFD·186/221\", EquipmentRepresentativeBoundaryKinds.AgileDirectPlayAttach)",
            "new(\"SFD·002/221\", EquipmentRepresentativeBoundaryKinds.TemperedOptionalAttach)",
            "new(\"SFD·008/221\", EquipmentRepresentativeBoundaryKinds.TemperedOptionalAttach)",
            "new(\"SFD·119/221\", EquipmentRepresentativeBoundaryKinds.TemperedOptionalAttach)",
            "new(\"SFD·119a/221\", EquipmentRepresentativeBoundaryKinds.TemperedOptionalAttach)",
            "new(\"SFD·186/221\", EquipmentRepresentativeBoundaryKinds.TemperedOptionalAttachEquipment)",
            "new(\"SFD·085/221\", EquipmentRepresentativeBoundaryKinds.FriendlyEquipmentStaticPower)",
            "new(\"SFD·085a/221\", EquipmentRepresentativeBoundaryKinds.FriendlyEquipmentStaticPower)"
        })
        {
            Assert.DoesNotContain(row, rulesSource, StringComparison.Ordinal);
        }

        Assert.True(CardEquipmentKeywordRules.HasAgileDirectPlayAttachRepresentativeBoundary("SFD·022/221"));
        Assert.True(CardEquipmentKeywordRules.HasAgileDirectPlayAttachRepresentativeBoundary("SFD·056/221"));
        Assert.True(CardEquipmentKeywordRules.HasAgileDirectPlayAttachRepresentativeBoundary("SFD·064/221"));
        Assert.True(CardEquipmentKeywordRules.HasAgileDirectPlayAttachRepresentativeBoundary("SFD·186/221"));
        Assert.False(CardEquipmentKeywordRules.HasAgileDirectPlayAttachRepresentativeBoundary("SFD·033/221"));
        Assert.True(CardEquipmentKeywordRules.HasTemperedOptionalAttachRepresentativeBoundary("SFD·002/221"));
        Assert.True(CardEquipmentKeywordRules.HasTemperedOptionalAttachRepresentativeBoundary("SFD·008/221"));
        Assert.True(CardEquipmentKeywordRules.HasTemperedOptionalAttachRepresentativeBoundary("SFD·085/221"));
        Assert.True(CardEquipmentKeywordRules.HasTemperedOptionalAttachRepresentativeBoundary("SFD·119/221"));
        Assert.True(CardEquipmentKeywordRules.HasTemperedOptionalAttachRepresentativeBoundary("SFD·119a/221"));
        Assert.False(CardEquipmentKeywordRules.HasTemperedOptionalAttachRepresentativeBoundary("SFD·033/221"));
        Assert.True(CardEquipmentKeywordRules.CanBeTemperedOptionalAttachEquipment("SFD·022/221"));
        Assert.True(CardEquipmentKeywordRules.CanBeTemperedOptionalAttachEquipment("SFD·056/221"));
        Assert.True(CardEquipmentKeywordRules.CanBeTemperedOptionalAttachEquipment("SFD·064/221"));
        Assert.True(CardEquipmentKeywordRules.CanBeTemperedOptionalAttachEquipment("SFD·186/221"));
        Assert.False(CardEquipmentKeywordRules.CanBeTemperedOptionalAttachEquipment("SFD·190/221"));
        Assert.True(CardEquipmentKeywordRules.HasFriendlyEquipmentStaticPowerRepresentativeBoundary("SFD·085/221"));
        Assert.True(CardEquipmentKeywordRules.HasFriendlyEquipmentStaticPowerRepresentativeBoundary("SFD·085a/221"));
        Assert.False(CardEquipmentKeywordRules.HasFriendlyEquipmentStaticPowerRepresentativeBoundary("SFD·033/221"));
        Assert.True(CardEquipmentKeywordRules.TryGetEquipmentStateRepresentative("SFD·022/221", out var stateRepresentative));
        Assert.Equal("Long Sword", stateRepresentative.CardName);
        Assert.False(CardEquipmentKeywordRules.TryGetEquipmentStateRepresentative("SFD·033/221", out _));
    }

    [Fact]
    public void TemperedOptionalAttachEquipmentChoiceDoesNotUseRuntimeSpinningAxeCardNumberConstant()
    {
        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var matchSessionPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "MatchSession.cs");

        Assert.DoesNotContain("SpinningAxeCardNo", File.ReadAllText(coreRuleEnginePath), StringComparison.Ordinal);
        Assert.DoesNotContain("SpinningAxeCardNo", File.ReadAllText(matchSessionPath), StringComparison.Ordinal);
    }

    [Fact]
    public void TemperedOptionalAttachSourceBoundaryDoesNotUseRuntimeSentinelAdeptCardNumberConstant()
    {
        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var matchSessionPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "MatchSession.cs");

        Assert.DoesNotContain("SentinelAdeptCardNo", File.ReadAllText(coreRuleEnginePath), StringComparison.Ordinal);
        Assert.DoesNotContain("SentinelAdeptCardNo", File.ReadAllText(matchSessionPath), StringComparison.Ordinal);
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
