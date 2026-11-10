namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // The original capture also determines which skills must still exist.
    // Checking only the surviving objects cannot detect an erased grant.
    internal static bool RequiresObservedUnitEventHistory(UnitEventContext? context)
        => context?.Timing is UnitEventTimings.PrintedPowerCardPlayed or UnitEventTimings.BoonConsumed
                or UnitEventTimings.PlayerDefend or UnitEventTimings.FriendlyUnitTargeted
                or ArmamentLastBreathRules.Timing or "FRIENDLY_UNIT_PLAYED" or "START" or OpponentScoringActionTiming
            || context?.Timing == "MOVE" && SelectedUnitEventSpec(context) is { RequiresMoveToBattlefield: true } or { PreservesMovementCapture: true }
            || context?.Origin?.ArmamentGranted == true
            || context is not null && HasSourcePlayCondition(UnitEventSpec(context.CardNo))
            || context?.TriggeringBattlefield is not null || context?.DelayedReceipt is not null
            || HasArmamentPlayAttachment(context);

    internal static string ObservedUnitEventCaptureHash(UnitEventContext context, string source, string controller)
    {
        var observation = new { source, controller, context.CardNo, context.Timing,
            context.SourceGeneration, context.FaceRevision, context.CapturedTick,
            context.TokenFactoryCardNo, context.Origin, context.ArmamentDeath, context.DefendedBattlefield,
            context.TargetSelection, context.Other, context.Here, context.PlayedUnitDuringSpellDuel };
        var capture = context.SourcePlayCondition is null ? MatchStateHasher.HashValue(observation)
            : MatchStateHasher.HashValue(new { observation, context.SourcePlayCondition });
        if (context.SourceConqueredBeforeEnd is not null)
            capture = MatchStateHasher.HashValue(new { capture, context.SourceConqueredBeforeEnd });
        if (context.ScoringAction is not null)
            capture = MatchStateHasher.HashValue(new { capture, context.ScoringAction });
        if (context.TriggeringBattlefield is not null || context.DelayedReceipt is not null)
            capture = MatchStateHasher.HashValue(new { capture, context.TriggeringBattlefield, context.DelayedReceipt });
        return HasArmamentPlayAttachment(context)
            ? MatchStateHasher.HashValue(new { hash = capture, context.ReflexiveTick }) : capture;
    }
}
