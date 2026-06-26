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
