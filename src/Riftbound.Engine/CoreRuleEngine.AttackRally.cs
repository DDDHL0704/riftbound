using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string AttackRallyKind = "ATTACK_MOVE_CONTROLLED_UNIT_TO_SOURCE_BATTLEFIELD";

    private static TriggerSpec? AttackRallySpec(string? cardNo)
        => SourceUnitPlayedTriggerSpecRules.TriggersForCard(cardNo).FirstOrDefault(spec =>
            spec.Kind == TriggerKinds.UnitAttackMoveControlledUnitToSourceBattlefield
            && spec.Timing == TriggerTimings.UnitAttack && spec.Optional == true
            && spec.TargetScope == TriggerTargetScopes.ControlledUnitOnField
            && spec.MovementDestinationZone == "BATTLEFIELD");

    private static TriggerQueueItemState[] CaptureAttackRallyTriggers(MatchState state,
        IEnumerable<string> attackers, string battlefield, string timing = TimingStates.NeutralClosed,
        string? returnFocusPlayer = null)
    {
        var result = new List<TriggerQueueItemState>();
        foreach (var id in attackers.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (!state.CardObjects.TryGetValue(id, out var source) || !IsFaceUpNonStandbyUnit(source)
                || source.ControllerId is not { Length: > 0 } controller) continue;
            foreach (var instance in AttachedRuleTextRules.Instances(state, source))
            {
                if (AttackRallySpec(instance.CardNo) is null) continue;
                var context = new FieldTriggerContext(instance.CardNo, AttackRallyKind,
                    source.ObjectGeneration, battlefield, returnFocusPlayer) {
                    SourceLocation = new(controller, "BATTLEFIELD", battlefield), Origin = instance.Origin,
                    SourceTokenFactoryCardNo = IsTokenObject(source) ? source.TokenFactoryCardNo ?? source.CardNo : null };
                result.Add(new TriggerQueueItemState($"attack-rally-{state.Tick + 1}-{result.Count}",
                    controller, id, FieldTriggerEffect, "BATTLE_DECLARED", timing) {
                    FieldContext = context, SourceCardNo = instance.CardNo });
            }
        }
        return result.ToArray();
    }

    private static bool ValidAttackRallyShape(FieldTriggerContext context, string effect, string? cardNo)
        => effect == FieldTriggerEffect && context.Kind == AttackRallyKind && context.SourceGeneration >= 0
            && (cardNo is null || cardNo == context.CardNo) && AttackRallySpec(context.CardNo) is not null
            && !string.IsNullOrWhiteSpace(context.BattlefieldId) && context.SourceLocation is not null
            && context.AttackConditionFrame is null && context.RecycledBoon is null
            && context.EmpoweredTransition is null && context.AffectedUnit is null && !context.AffectedUnitCeasedToExist
            && (context.SourceTokenFactoryCardNo is null ? !context.SourceCeasedToExist
                : P6TokenFactoryCatalog.TryGetByCardNo(context.SourceTokenFactoryCardNo, out var token)
                    && token.Tags.Contains(CardObjectTags.UnitCard));

    internal static bool ValidAttackRallyStack(StackItemState item)
    {
        if (item.FieldContext is not { Kind: AttackRallyKind }) return true;
        return item.OptionalCosts.Count == 0
            && item.DamageAmount == 0
            && item.EffectRepeatCount == 1
            && item.Destination.Length == 0;
    }

    private static bool IsAttackRallyTarget(MatchState state, StackItemState item, CardObjectState target)
        => EffectiveFieldControllerId(state.PlayerZones, target.ObjectId, target) == item.ControllerId
            && CombatMightHere(state, item) is { Zone: "BATTLEFIELD", BattlefieldObjectId: { } field }
            && IsUnitMovementDestination(state, target.ObjectId, "BATTLEFIELD:" + field);

    private static StackResolutionResult ResolveAttackRally(MatchState state, StackItemState item)
    {
        if (item.FieldContext is not { Kind: AttackRallyKind } context
            || !ValidFieldContextState(state, context, item.SourceObjectId, item.ControllerId, item.EffectKind, item.CardNo))
            throw new InvalidOperationException("Invalid captured attack movement skill.");
        var result = NoopStackResolutionResult(state);
        var events = new List<GameEvent> { new("TRIGGER_RESOLVED", "进攻移动技能结算", new Dictionary<string, object?> {
            ["controllerId"] = item.ControllerId, ["effectKind"] = item.EffectKind,
            ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__") }) };
        // CN 359.3.f.2: the source's battlefield is a reference checked now.
        // A source in base or outside the field supplies no battlefield.
        if (item.TargetObjectIds.SingleOrDefault() is not { } target
            || item.TargetGenerations?.GetValueOrDefault(target, -1) != state.CardObjects.GetValueOrDefault(target)?.ObjectGeneration
            || !FieldTriggerTargets(state, item).Contains(target)
            || CombatMightHere(state, item) is not { Zone: "BATTLEFIELD", BattlefieldObjectId: { } field })
            return result with { Events = events };
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var locations = state.ObjectLocations.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var pools = state.RunePools.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        MoveFieldUnitsToChosenLocation(state, zones, cards, locations, pools, item, [target], "BATTLEFIELD:" + field, events);
        var cleanup = RunStateBasedCleanupLoop(state, zones, cards, item, pools,
            objectLocations: locations, destroyedUnitOwnerIdsAlreadyThisTurn: state.DestroyedUnitOwnerIdsThisTurn);
        events.AddRange(cleanup.Events);
        return result with { PlayerZones = zones, CardObjects = cards, RunePools = cleanup.RunePools,
            TriggerQueue = cleanup.TriggerQueue, DestroyedUnitOwnerIds = cleanup.DestroyedUnitOwnerIds,
            ObjectLocations = ReconcileObjectLocations(locations, zones), Events = events };
    }
}
