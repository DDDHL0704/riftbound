using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class PrimitiveTargetGuardSourceTests
{
    [Fact]
    public void PlayCardTargetGuardsUseSharedBehaviorPrimitiveRules()
    {
        var repositoryRoot = RepositoryRoot();
        var coreRuleEngineSource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Riftbound.Engine",
            "CoreRuleEngine.cs"));
        var matchSessionSource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Riftbound.Engine",
            "MatchSession.cs"));

        foreach (var methodName in RepresentativeTargetGuardMethodNames)
        {
            Assert.DoesNotContain(methodName, coreRuleEngineSource, StringComparison.Ordinal);
        }

        foreach (var effectKind in RepresentativeTargetGuardEffectKinds)
        {
            Assert.DoesNotContain($"\"{effectKind}\"", coreRuleEngineSource, StringComparison.Ordinal);
            Assert.DoesNotContain($"\"{effectKind}\"", matchSessionSource, StringComparison.Ordinal);
        }

        Assert.Contains("RequiresVisibleFieldUnitPrimitiveTarget", coreRuleEngineSource, StringComparison.Ordinal);
        Assert.Contains("RequiresVisibleFieldUnitPrimitiveTarget", matchSessionSource, StringComparison.Ordinal);
        Assert.Contains("RequiresPublicUnitMainDeckPrimitiveTarget", coreRuleEngineSource, StringComparison.Ordinal);
    }

    private static readonly string[] RepresentativeTargetGuardMethodNames =
    [
        "IsBattleOrFlightTargetAllowed",
        "IsGustTargetAllowed",
        "IsHuntTheWeakTargetAllowed",
        "IsRideTheWindTargetAllowed",
        "IsCharmTargetAllowed",
        "IsIsolateTargetAllowed",
        "IsVengeanceTargetAllowed",
        "IsHostileTakeoverTargetAllowed",
        "IsSwitcherooTargetAllowed",
        "IsSpiritFireTargetAllowed",
        "IsPromptSpiritFireTargetAllowed",
        "IsZenithBladeTargetAllowed",
        "IsBerserkImpulseTargetAllowed"
    ];

    private static readonly string[] RepresentativeTargetGuardEffectKinds =
    [
        "BATTLE_OR_FLIGHT_MOVE_BATTLEFIELD_UNIT_TO_BASE",
        "GUST_RETURN_BATTLEFIELD_UNIT_POWER_3_OR_LESS_TO_HAND",
        "HUNT_THE_WEAK_DESTROY_BATTLEFIELD_UNIT_POWER_3_OR_LESS",
        "RIDE_THE_WIND_MOVE_FRIENDLY_BATTLEFIELD_UNIT_TO_BASE_READY",
        "CHARM_MOVE_ENEMY_BATTLEFIELD_UNIT_TO_BASE",
        "ISOLATE_MOVE_ENEMY_BATTLEFIELD_UNIT_TO_BASE_NO_DRAW",
        "VENGEANCE_DESTROY_UNIT",
        "HOSTILE_TAKEOVER_GAIN_CONTROL_READY_ENEMY_BATTLEFIELD_UNIT",
        "SWITCHEROO_SWAP_TWO_BATTLEFIELD_UNIT_POWERS",
        "SPIRIT_FIRE_DESTROY_BATTLEFIELD_UNITS_TOTAL_POWER_4",
        "ZENITH_BLADE_STUN_ENEMY_BATTLEFIELD_UNIT_NO_MOVE",
        "BERSERK_IMPULSE_PLAY_OPPONENT_TOP_UNIT"
    ];

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
