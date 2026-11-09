namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // The original capture also determines which skills must still exist.
    // Checking only the surviving objects cannot detect an erased grant.
    internal static bool RequiresObservedUnitEventHistory(UnitEventContext? context)
        => context?.Timing is UnitEventTimings.PrintedPowerCardPlayed or UnitEventTimings.BoonConsumed
                or UnitEventTimings.PlayerDefend or UnitEventTimings.FriendlyUnitTargeted
                or ArmamentLastBreathRules.Timing or "FRIENDLY_UNIT_PLAYED"
            || context?.Timing == "MOVE" && SelectedUnitEventSpec(context) is { RequiresMoveToBattlefield: true } or { PreservesMovementCapture: true }
            || context?.Origin?.ArmamentGranted == true
            || context?.TriggeringBattlefield is not null || context?.DelayedReceipt is not null;

    internal static string ObservedUnitEventCaptureHash(UnitEventContext context, string source, string controller)
    {
        var capture = MatchStateHasher.HashValue(new { source, controller, context.CardNo, context.Timing,
            context.SourceGeneration, context.FaceRevision, context.CapturedTick,
            context.TokenFactoryCardNo, context.Origin, context.ArmamentDeath, context.DefendedBattlefield,
            context.TargetSelection, context.Other, context.Here, context.PlayedUnitDuringSpellDuel, context.SourceConqueredBeforeEnd });
        return context.TriggeringBattlefield is null && context.DelayedReceipt is null ? capture
            : MatchStateHasher.HashValue(new { capture, context.TriggeringBattlefield, context.DelayedReceipt });
    }
}
