using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // Public chosen targets of a permanent's play trigger belong to that
    // independent ability (CN 355.5.b), after the permanent enters the field.
    // Private hand/deck decisions and target-dependent effect costs use their
    // separate continuation protocols and are not covered by this primitive.
    internal static bool HasIndependentPermanentTargets(CardBehaviorDefinition behavior)
        => (behavior.PlaysSourceToBaseAsUnit || behavior.PlaysSourceToBaseAsEquipment)
            && (OfficialPrintedTimingRules.HasPlayTrigger(behavior.CardNo) || HasStandbyCountedPlayAbility(behavior) || HasStandbyAttachmentPlayAbility(behavior))
            && behavior.RequiredTargetCount > 0 && !IsDeferredDeckChoice(behavior)
            && behavior.TargetEffectAdditionalManaCost == 0 && behavior.TargetEffectAdditionalPowerCost == 0
            && behavior.TargetScope is not (CardTargetScopes.FriendlyHandCard or CardTargetScopes.OpponentHandCard
                or CardTargetScopes.AnyHandCard or CardTargetScopes.FriendlyMainDeckCard);

    private static string[] PermanentPlayTargetChoices(MatchState state, StackItemState item)
    {
        var behavior = PermanentPlayTargetBehavior(item);
        var maximum = MaxResolvedTargetCount(behavior, item, TargetCountConditionApplies(behavior, item));
        var scope = PlayCardTargetScopeForBehavior(behavior);
        return state.CardObjects.Keys.Where(id => (!OfficialPrintedTimingRules.PlayTargetsExcludeSource(item.CardNo) || id != item.SourceObjectId)
            && PermanentPlayHereTargetAllowed(state, item, behavior, id)
            && (!behavior.SwapsSourceWithFirstTargetLocation || CanSwapTargetLocations(state.PlayerZones, state.CardObjects, item.SourceObjectId, id))
            && Enumerable.Range(0, maximum)
                .Any(index => IsLegalChosenCardTarget(state, item.ControllerId, id, index, [id], scope, behavior)))
            .Order(StringComparer.Ordinal).ToArray();
    }

    private static CardBehaviorDefinition PermanentPlayTargetBehavior(StackItemState item)
    {
        CardBehaviorRegistry.TryGetByEffectKind(item.EffectKind, out var behavior);
        return StandbyDefinition(PermanentPlayDefinition(behavior!, IsStackItemBattlefieldDestination(item)), item.PlayedFromStandby);
    }

    private static bool ValidPermanentPlayTargetSelection(MatchState state, StackItemState item, IReadOnlyList<string> selected)
    {
        var behavior = PermanentPlayTargetBehavior(item);
        var candidate = item with { TargetObjectIds = selected };
        return HasValidResolvedTargetCount(behavior, candidate)
            && (!OfficialPrintedTimingRules.PlayTargetsExcludeSource(item.CardNo) || !selected.Contains(item.SourceObjectId))
            && PlayCardTargetsExposeKnownCardNumbers(state, selected)
            && HasValidTargetGroup(state, behavior, selected) && HasRequiredAnyTargetTag(state, behavior, selected)
            && selected.All(id => PermanentPlayHereTargetAllowed(state, item, behavior, id))
            && (!behavior.SwapsSourceWithFirstTargetLocation || selected.All(id => CanSwapTargetLocations(state.PlayerZones, state.CardObjects, item.SourceObjectId, id)))
            && selected.Select((id, index) => IsLegalChosenCardTarget(state, item.ControllerId, id, index,
                selected, PlayCardTargetScopeForBehavior(behavior), behavior)).All(legal => legal);
    }

    private static PendingCardChoiceState PermanentPlayTargetChoice(MatchState state, StackItemState item)
    {
        var behavior = PermanentPlayTargetBehavior(item);
        var condition = TargetCountConditionApplies(behavior, item);
        var legal = PermanentPlayTargetChoices(state, item);
        return new("PERMANENT-TARGET:" + item.StackItemId, "TRIGGER_CONFIRMATION", item.ControllerId,
            MinTargetCount(behavior, condition), MaxResolvedTargetCount(behavior, item, condition), legal, legal,
            "常驻牌已入场：选择打出技能的目标并确认法盾费用，随后双方可以响应。",
            item.SourceObjectId, item.EffectKind) { ResolvingStackItemId = item.StackItemId };
    }

    private static ResolutionResult PreparePermanentPlayTargets(ResolutionResult result, StackItemState item)
    {
        var choice = PermanentPlayTargetChoice(result.State, item);
        bool HasSelection(List<string> selected)
            => selected.Count == choice.RequiredCount
                ? ValidPermanentPlayTargetSelection(result.State, item, selected)
                : choice.LegalObjectIds.Where(id => !selected.Contains(id)).Any(id => HasSelection([.. selected, id]));
        if (choice.MaxCount == 0 || choice.LegalObjectIds.Count == 0 || choice.LegalObjectIds.Count < choice.RequiredCount
            || !HasSelection([]))
            return PrepareTriggerConfirmation(DiscardUnconfirmedTrigger(result, item));
        var state = result.State with { PendingCardChoice = choice, PriorityPlayerId = null, ActivePlayerId = item.ControllerId };
        var pending = result with { State = state, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
        // A command may carry a checked selection intent. Confirm it again
        // against the post-entry field, then bind its current generations.
        if (item.TargetObjectIds.Count == 0 || !ValidPermanentPlayTargetSelection(state, item, item.TargetObjectIds)) return pending;
        var confirmed = ResolveTriggerTargetConfirmation(state, choice, item.TargetObjectIds, advanceTick: false);
        return confirmed with { Events = result.Events.Concat(confirmed.Events).ToArray() };
    }

    private static bool ValidPermanentPlayTargetChoice(MatchState state, PendingCardChoiceState choice)
    {
        var item = state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId);
        if (item is not { PermanentPlayTargetsPending: true, TargetGenerations: null }
            || !ValidPermanentPlayTargetStack(state, item)) return false;
        var expected = PermanentPlayTargetChoice(state, item);
        return choice.ChoiceWindow == expected.ChoiceWindow && choice.ChoiceId == expected.ChoiceId
            && choice.PlayerId == expected.PlayerId && choice.SourceObjectId == expected.SourceObjectId
            && choice.EffectKind == expected.EffectKind && choice.RequiredCount == expected.RequiredCount
            && choice.MaxCount == expected.MaxCount && choice.LegalObjectIds.SequenceEqual(expected.LegalObjectIds)
            && choice.ContextObjectIds.SequenceEqual(expected.ContextObjectIds);
    }

    internal static bool ValidPermanentPlayTargetStack(MatchState state, StackItemState item)
        => !item.PermanentPlayTargetsPending || item.SourceConfirmed
            && CardBehaviorRegistry.TryGetByEffectKind(item.EffectKind, out var behavior) && behavior.CardNo == item.CardNo
            && HasIndependentPermanentTargets(StandbyDefinition(behavior, item.PlayedFromStandby)) && state.PriorityPlayerId is null
            && (state.PendingCardChoice is not null || state.PendingPayment is not null || state.TriggerQueue.Count > 0)
            && (item.TargetGenerations is null || state.PendingPayment is { PaymentWindow: TriggerTargetCostWindow } payment
                && payment.ResolvingStackItemId == item.StackItemId && item.TargetGenerations.Count == item.TargetObjectIds.Count
                && item.TargetObjectIds.All(id => item.TargetGenerations.TryGetValue(id, out var generation) && generation >= 0));
}
