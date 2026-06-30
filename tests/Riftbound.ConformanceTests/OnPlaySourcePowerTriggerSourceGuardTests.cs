using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OnPlaySourcePowerTriggerSourceGuardTests
{
    [Fact]
    public void QueuedOnPlaySourcePowerTriggerUsesBehaviorFieldsInsteadOfEffectKindName()
    {
        var coreRuleEnginePath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs");
        var coreRuleEngineSource = File.ReadAllText(coreRuleEnginePath);

        const string methodName = "private static bool IsQueuedOnPlaySourcePowerTrigger";
        var methodStart = coreRuleEngineSource.IndexOf(methodName, StringComparison.Ordinal);
        Assert.True(methodStart >= 0, $"{methodName} should exist.");

        const string nextMethodName = "private static TriggerQueueItemState BuildOnPlayTriggerQueueItem";
        var nextMethodStart = coreRuleEngineSource.IndexOf(nextMethodName, methodStart, StringComparison.Ordinal);
        Assert.True(nextMethodStart > methodStart, $"{methodName} should stay before {nextMethodName}.");

        var methodSource = coreRuleEngineSource[methodStart..nextMethodStart];

        Assert.Contains("behavior.PlaysSourceToBaseAsUnit", methodSource, StringComparison.Ordinal);
        Assert.Contains("behavior.AppliesPowerModifierToSourceUnit", methodSource, StringComparison.Ordinal);
        Assert.Contains("behavior.PowerModifierAmount != 0", methodSource, StringComparison.Ordinal);
        Assert.DoesNotContain("EffectKind.Contains", methodSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TEEMO", methodSource, StringComparison.Ordinal);
        Assert.DoesNotContain("PLAY_UNIT_SELF_POWER_PLUS_3", methodSource, StringComparison.Ordinal);
    }

    [Fact]
    public void RecoveryValidatorUsesBehaviorFieldsForOnPlaySourcePowerSourceCardValidation()
    {
        var matchRecoveryPath = Path.Combine(
            RepositoryRoot(),
            "src",
            "Riftbound.Engine",
            "MatchRecovery.cs");
        var matchRecoverySource = File.ReadAllText(matchRecoveryPath);

        Assert.Contains(
            "OnPlaySourcePowerSourceTriggerSpecLabelForRecovery",
            matchRecoverySource,
            StringComparison.Ordinal);
        Assert.Contains(
            "CardBehaviorRegistry.IsOnPlaySourcePowerTriggerSource",
            matchRecoverySource,
            StringComparison.Ordinal);
        Assert.DoesNotContain("TeemoOnPlaySelfPowerCardNoForRecovery", matchRecoverySource, StringComparison.Ordinal);
        Assert.DoesNotContain("TeemoAltAOnPlaySelfPowerCardNoForRecovery", matchRecoverySource, StringComparison.Ordinal);
        Assert.DoesNotContain("TeemoAltBOnPlaySelfPowerCardNoForRecovery", matchRecoverySource, StringComparison.Ordinal);
        Assert.DoesNotContain("FndTeemoOnPlaySelfPowerCardNoForRecovery", matchRecoverySource, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTeemoOnPlaySelfPowerCardNoForRecovery", matchRecoverySource, StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(directory))
        {
            if (File.Exists(Path.Combine(directory, "riftbound-dotnet.sln"))
                || File.Exists(Path.Combine(directory, "riftbound.slnx")))
            {
                return directory;
            }

            var parent = Directory.GetParent(directory);
            if (parent is null)
            {
                break;
            }

            directory = parent.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
