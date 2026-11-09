namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // The original capture also determines which skills must still exist.
    // Checking only the surviving objects cannot detect an erased grant.
    internal static bool RequiresObservedUnitEventHistory(UnitEventContext? context)
        => context?.Timing is UnitEventTimings.PrintedPowerCardPlayed or UnitEventTimings.BoonConsumed
                or UnitEventTimings.PlayerDefend or UnitEventTimings.FriendlyUnitTargeted
                or ArmamentLastBreathRules.Timing
            || context?.Timing == "MOVE" && SelectedUnitEventSpec(context)?.RequiresMoveToBattlefield == true
            || context?.Origin?.ArmamentGranted == true;

    internal static string ObservedUnitEventCaptureHash(UnitEventContext context, string source, string controller)
        => MatchStateHasher.HashValue(new { source, controller, context.CardNo, context.Timing,
            context.SourceGeneration, context.FaceRevision, context.CapturedTick,
            context.TokenFactoryCardNo, context.Origin, context.ArmamentDeath, context.DefendedBattlefield,
            context.TargetSelection, context.Other, context.Here });
}
