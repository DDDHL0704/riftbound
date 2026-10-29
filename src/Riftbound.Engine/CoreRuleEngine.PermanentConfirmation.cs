using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static bool HasPendingPermanentPlayAbility(MatchState state, StackItemState item, CardBehaviorDefinition behavior)
        => (OfficialPrintedTimingRules.HasPlayTrigger(behavior.CardNo, IsStackItemBattlefieldDestination(item))
                && SourcePlayDrawSpec(behavior.CardNo) is null
                && PaidUnitStunSpec(behavior.CardNo) is null
                && !SourceUnitTokenSpecs.Value.ContainsKey(behavior.CardNo)
                && !ReadyOtherObjectSpecs.Value.ContainsKey(behavior.CardNo)
                && (behavior.CreatedBaseUnitTokenConditionKind != CardTokenCreationConditionKinds.DiscardHandCardOptionalCostPaid
                    || ShouldCreateBaseUnitTokens(behavior, item))
                && behavior.EmpowerInteraction?.Timing is not ("PLAY" or "STANDBY_PLAY")
                && (!HasTargetEffectAdditionalCost(behavior) || TargetEffectAdditionalCostPaid(behavior, item.OptionalCosts)))
            || HasStandbyCountedPlayAbility(StandbyDefinition(behavior, item.PlayedFromStandby))
            || HasStandbyAttachmentPlayAbility(StandbyDefinition(behavior, item.PlayedFromStandby))
            || (behavior.MainDeckLookCount > 0 && behavior.RecyclesSelectedMainDeckTargets);

    // CN 359.2: confirming a permanent is part of PLAY_CARD, not a pair of passes.
    private static ResolutionResult ConfirmPlayedPermanent(MatchState before, MatchState paid,
        PlayerIntent intent, IReadOnlyList<GameEvent> playEvents)
    {
        var confirmation = ResolvePassPriority(paid with { Tick = before.Tick }, intent, confirmPermanent: true);
        var events = playEvents.Where(e => e.Kind != "STACK_ITEM_ADDED").Concat(confirmation.Events).ToArray();
        return confirmation with { Events = events };
    }

    private static StackResolutionResult PendingPermanentPlayAbility(MatchState state, StackItemState item,
        Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards, List<GameEvent> events)
    {
        var ability = item with { SourceConfirmed = true };
        if (CardBehaviorRegistry.TryGetByCardNo(item.CardNo, out var copyBehavior)
            && copyBehavior.CreatedBaseUnitTokensCopySource)
            ability = ability with { SelfCopySource = new(item.SourceObjectId, cards[item.SourceObjectId].ObjectGeneration) };
        if (CardBehaviorRegistry.TryGetByEffectKind(item.EffectKind, out var targeted)
            && HasIndependentPermanentTargets(StandbyDefinition(
                PermanentPlayDefinition(targeted, IsStackItemBattlefieldDestination(item)), item.PlayedFromStandby)))
            ability = ability with { PermanentPlayTargetsPending = true, TargetGenerations = null };
        if (targeted?.TargetScope == CardTargetScopes.EnemyUnitAtSourceLocation && IsStackItemBattlefieldDestination(item))
            ability = ability with { PermanentPlaySource = new(item.SourceObjectId, cards[item.SourceObjectId].ObjectGeneration) };
        if (CardBehaviorRegistry.TryGetByCardNo(item.CardNo, out var behavior) && behavior.PlayDamagePerLocation > 0)
            ability = ability with { TargetObjectIds = [], TargetGenerations = null, LocationDamage = new(item.CardNo) };
        events.Add(new("TRIGGER_QUEUED", "入场后的打出技能等待响应", new Dictionary<string, object?>
        { ["sourceObjectId"] = item.SourceObjectId, ["controllerId"] = item.ControllerId,
            ["stackItemId"] = ability.StackItemId, ["effectKind"] = item.EffectKind }));
        var locations = ReconcileObjectLocations(state.ObjectLocations, zones);
        if (item.PlayedFromStandby is { } shadow) locations[item.SourceObjectId] = new(item.ControllerId, "BATTLEFIELD", shadow.Battlefield.ObjectId);
        return new(zones, cards, state.PlayerScores, state.PlayerExperience, state.RunePools,
            state.UntilEndOfTurnEffects, null, events, [],
            state.StackItems.Where(x => x.StackItemId != item.StackItemId).Append(ability).ToArray(),
            [], null, [], state.RngCursor,
            ObjectLocations: locations);
    }
}
