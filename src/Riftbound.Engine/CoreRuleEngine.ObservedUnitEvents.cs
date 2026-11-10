namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // The original capture also determines which skills must still exist.
    // Checking only the surviving objects cannot detect an erased grant.
    internal static bool RequiresObservedUnitEventHistory(UnitEventContext? context)
        => context?.Timing == AgilePlayTiming || HasArmamentPlayAttachment(context)
            || context?.Timing is UnitEventTimings.PrintedPowerCardPlayed or UnitEventTimings.BoonConsumed
                or UnitEventTimings.PlayerDefend or UnitEventTimings.FriendlyUnitTargeted
                or ArmamentLastBreathRules.Timing or FirstFriendlyStartPhaseDeathTiming or SpellInstructionReflexiveTiming or "FRIENDLY_UNIT_PLAYED" or "START" or OpponentScoringActionTiming
            || context?.Timing == "MOVE" && SelectedUnitEventSpec(context) is { RequiresMoveToBattlefield: true } or { PreservesMovementCapture: true }
            || context is not null && SelectedUnitEventSpec(context)?.RequiredFriendlyUnitTagsAtTrigger is not null
            || context?.AssignedOverkill is not null || context?.RequiredUnitDestruction is not null
            || context?.Origin?.ArmamentGranted == true
            || context is not null && HasSourcePlayCondition(UnitEventSpecForContext(context))
            || context?.Timing == "PLAY" && (UnitEventRequiresOpponent(SelectedUnitEventSpec(context))
                || SelectedUnitEventSpec(context) is { ReturnAllUnitsMaximumMight: not null }
                    or { ExemptsControllerIfExperiencePaid: true } or { GraveyardReturnAnyTags: not null })
            || context is not null && UnitEventSpecForContext(context) is { Effect: UnitEventLookUnitEffect, DeckSelection: not null }
            || context?.TriggeringBattlefield is not null || context?.DelayedReceipt is not null;

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
        if (context.RequiredUnitDestruction is not null || context.StartPhaseUnitDeath is not null)
            capture = MatchStateHasher.HashValue(new { capture, context.RequiredUnitDestruction, context.StartPhaseUnitDeath });
        if (context.SourceOptionalExperiencePaid)
            capture = MatchStateHasher.HashValue(new { capture, context.SourceOptionalExperiencePaid });
        if (context.AssignedOverkill is not null)
            capture = MatchStateHasher.HashValue(new { capture, context.AssignedOverkill });
        if (context.SpellInstruction is not null)
            capture = MatchStateHasher.HashValue(new { capture, context.SpellInstruction });
        if (context.PaidLegendPlayCost is not null)
            capture = MatchStateHasher.HashValue(new { capture, context.PaidLegendPlayCost });
        if (context.RevealedDeckSelection is not null)
            capture = MatchStateHasher.HashValue(new { capture, context.RevealedDeckSelection });
        if (context.AgilePlayGrant is not null)
            capture = MatchStateHasher.HashValue(new { capture, context.AgilePlayGrant });
        if (context.TriggeringBattlefield is not null || context.DelayedReceipt is not null)
            capture = MatchStateHasher.HashValue(new { capture, context.TriggeringBattlefield, context.DelayedReceipt });
        if (context.HandExile is not null)
            capture = MatchStateHasher.HashValue(new { capture, context.HandExile });
        return HasArmamentPlayAttachment(context)
            ? MatchStateHasher.HashValue(new { hash = capture, context.ReflexiveTick }) : capture;
    }
}
