using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class TeemoLegendActionDomainGuardTests
{
    private static readonly string[] ExpectedTeemoUnitCardNos =
    {
        "FND-196/298",
        "OGN·121/298",
        "OGN·121a/298",
        "OGN·197/298",
        "OGN·197a/298",
        "OGN·197b/298",
        "SFD·230/221",
        "SFD·230*/221"
    };

    public static TheoryData<string> TeemoUnitCardNos
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var cardNo in ExpectedTeemoUnitCardNos)
            {
                data.Add(cardNo);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(TeemoUnitCardNos))]
    public void UnitIdentityCatalogIdentifiesImplementedTeemoUnitsByCatalogIdentity(string cardNo)
    {
        Assert.True(UnitIdentityCatalog.IsSourceCardNoForIdentity(UnitIdentityCatalog.TeemoUnitIdentityId, cardNo));
    }

    [Theory]
    [InlineData("OGN·263/298")]
    [InlineData("OGN·307/298")]
    [InlineData("SFD·082/221")]
    public void UnitIdentityCatalogDoesNotTreatLegendsOrOtherUnitsAsTeemoUnits(string cardNo)
    {
        Assert.False(UnitIdentityCatalog.IsSourceCardNoForIdentity(UnitIdentityCatalog.TeemoUnitIdentityId, cardNo));
    }

    [Fact]
    public void UnitIdentityCatalogUsesImplementedBehaviorRowsForTeemoUnitIdentity()
    {
        Assert.Equal(
            ExpectedTeemoUnitCardNos.OrderBy(cardNo => cardNo, StringComparer.Ordinal),
            UnitIdentityCatalog.SourceCardNosForIdentity(UnitIdentityCatalog.TeemoUnitIdentityId)
                .OrderBy(cardNo => cardNo, StringComparer.Ordinal));
    }

    [Fact]
    public void TeemoLegendActionDomainDoesNotUseDuplicatedCardNumberAllowLists()
    {
        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        Assert.DoesNotContain("IsTeemoUnitCardNo", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("FND-196/298", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("OGN·121/298", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("OGN·197b/298", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SFD·230*/221", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("\"提莫\"", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CardBehaviorRegistry.IsImplementedUnitNamed", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("UnitIdentityCatalog.IsSourceCardNoForIdentity", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("UnitIdentityCatalog.TeemoUnitIdentityId", coreRuleEngineSource, StringComparison.Ordinal);

        var matchSessionPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "MatchSession.cs");
        var matchSessionSource = File.ReadAllText(matchSessionPath);

        Assert.DoesNotContain("LegendActionIsTeemoUnitCardNo", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsTeemoLegendCardNo", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsImplementedLegendActionCardNo", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("\"提莫\"", matchSessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CardBehaviorRegistry.IsImplementedUnitNamed", matchSessionSource, StringComparison.Ordinal);
        Assert.Contains("HasImplementedLegendActionAbility", matchSessionSource, StringComparison.Ordinal);
        Assert.Contains("TeemoLegendAbilityId", matchSessionSource, StringComparison.Ordinal);
        Assert.Contains("UnitIdentityCatalog.IsSourceCardNoForIdentity", matchSessionSource, StringComparison.Ordinal);
        Assert.Contains("UnitIdentityCatalog.TeemoUnitIdentityId", matchSessionSource, StringComparison.Ordinal);
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
