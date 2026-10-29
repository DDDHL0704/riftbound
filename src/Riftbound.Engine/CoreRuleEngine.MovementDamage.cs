using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static bool HasTargetedMovementSkill(StackItemState item)
        => item.MovementObserver?.Kind is TriggerKinds.UnitMovedDamageAtOriginOrDestination or TriggerKinds.UnitMovedSetOtherUnitBaseMight;

    // CN 359.3.f.3: the origin and destination are properties of the movement
    // that triggered the skill, even if the source subsequently moves or leaves.
    private static string[] TargetedMovementTargets(MatchState state, StackItemState item)
    {
        var moved = item.MovementObserver!.Unit;
        var fields = new[] { moved.Origin, moved.Destination }
            .Where(location => location.Zone == "BATTLEFIELD" && !string.IsNullOrWhiteSpace(location.BattlefieldObjectId))
            .Select(location => location.BattlefieldObjectId!).ToHashSet(StringComparer.Ordinal);
        return state.CardObjects.Values.Where(card => IsFaceUpNonStandbyUnit(card)
            && IsObjectOnField(state.PlayerZones, card.ObjectId)
            && (item.MovementObserver.Kind == TriggerKinds.UnitMovedSetOtherUnitBaseMight
                ? card.ObjectId != item.SourceObjectId || card.ObjectGeneration != item.MovementObserver.SourceGeneration
                : state.ObjectLocations.TryGetValue(card.ObjectId, out var location)
                    && location.Zone == "BATTLEFIELD" && location.BattlefieldObjectId is not null && fields.Contains(location.BattlefieldObjectId))
            && TargetProtectionRules.IsLegalActivatedSkillTarget(state, item.ControllerId, card.ObjectId))
            .Select(card => card.ObjectId).Order(StringComparer.Ordinal).ToArray();
    }

    private static PendingCardChoiceState TargetedMovementChoice(MatchState state, StackItemState item)
    {
        var legal = TargetedMovementTargets(state, item);
        return new("MOVEMENT-DAMAGE:" + item.StackItemId, "TRIGGER_CONFIRMATION", item.ControllerId,
            0, 1, legal, legal, item.MovementObserver!.Kind == TriggerKinds.UnitMovedSetOtherUnitBaseMight
                ? "选择另一名单位，或不选以放弃；确认目标和法盾费用后双方可以响应。"
                : "选择移动起点或终点战场的一名单位，或不选以放弃；确认目标和法盾费用后双方可以响应。",
            item.SourceObjectId, item.EffectKind) { ResolvingStackItemId = item.StackItemId };
    }

    private static StackResolutionResult ResolveMovementBaseMight(MatchState state, StackItemState item, List<GameEvent> events)
    {
        if (!ValidTargetedMovementStack(state, item)) throw new InvalidOperationException("Invalid captured movement base Might skill.");
        var result = NoopStackResolutionResult(state);
        if (item.TargetObjectIds.SingleOrDefault() is not { } target
            || item.TargetGenerations?.GetValueOrDefault(target, -1) != state.CardObjects.GetValueOrDefault(target)?.ObjectGeneration
            || !TargetedMovementTargets(state, item).Contains(target)) return result with { Events = events };
        var spec = UnitMovedTriggerSpecRules.MovementTriggers(item.CardNo).Single(UnitMovedTriggerSpecRules.IsMovementBaseMightTrigger);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var unit = cards[target];
        var value = spec.BasePowerUntilEndOfTurn!.Value;
        cards[target] = TemporaryUnitEffectRules.SetNumber(unit, TemporaryUnitEffectRules.BaseMightPrefix, value, retainZero: true);
        events.Add(new("BASE_MIGHT_SET_UNTIL_END_OF_TURN", "设置单位本回合基础战力", new Dictionary<string, object?> {
            ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__"),
            ["targetObjectId"] = target, ["targetGeneration"] = unit.ObjectGeneration,
            ["baseMight"] = value, ["cardNo"] = item.CardNo }));
        var zones = NormalizeZonesForSeats(state);
        var locations = state.ObjectLocations.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var cleanup = RunStateBasedCleanupLoop(state, zones, cards, item, state.RunePools,
            objectLocations: locations,
            destroyedUnitOwnerIdsAlreadyThisTurn: state.DestroyedUnitOwnerIdsThisTurn);
        events.AddRange(cleanup.Events);
        return result with { PlayerZones = zones, CardObjects = cards, RunePools = cleanup.RunePools,
            TriggerQueue = cleanup.TriggerQueue, DestroyedUnitOwnerIds = cleanup.DestroyedUnitOwnerIds,
            ObjectLocations = ReconcileObjectLocations(locations, zones), Events = events };
    }

    private static ResolutionResult PrepareTargetedMovement(ResolutionResult result, StackItemState item)
    {
        var choice = TargetedMovementChoice(result.State, item);
        if (choice.LegalObjectIds.Count == 0) return PrepareTriggerConfirmation(DiscardUnconfirmedTrigger(result, item));
        var state = result.State with { PendingCardChoice = choice, PriorityPlayerId = null, ActivePlayerId = item.ControllerId };
        return result with { State = state, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static bool ValidTargetedMovementChoice(MatchState state, PendingCardChoiceState choice)
        => state.StackItems.FirstOrDefault(item => item.StackItemId == choice.ResolvingStackItemId) is { TargetGenerations: null } item
            && HasTargetedMovementSkill(item) && ValidTargetedMovementStack(state, item)
            && TargetedMovementTargets(state, item).Length > 0 && SameDistributionChoice(choice, TargetedMovementChoice(state, item));

    internal static bool ValidTargetedMovementStack(MatchState state, StackItemState item)
    {
        if (!HasTargetedMovementSkill(item)) return true;
        var context = item.MovementObserver!;
        if (!ValidMovementObserver(state, context, item.EffectKind, item.ControllerId, item.SourceObjectId, item.CardNo)
            || item.SourceConfirmed || item.EffectRepeatCount != 1 || item.OptionalCosts.Count != 0
            || item.DamageAmount != 0 || item.Destination.Length != 0) return false;
        if (item.TargetGenerations is null) return item.TargetObjectIds.Count == 0 && context.TargetTokenFactoryCardNo is null;
        if (item.TargetObjectIds.Count != 1 || item.TargetGenerations.Count != 1
            || string.IsNullOrWhiteSpace(item.TargetObjectIds[0])
            || !item.TargetGenerations.TryGetValue(item.TargetObjectIds[0], out var generation) || generation < 0) return false;
        if (context.Kind == TriggerKinds.UnitMovedSetOtherUnitBaseMight && item.TargetObjectIds[0] == item.SourceObjectId
            && generation == context.SourceGeneration) return false;
        var factory = context.TargetTokenFactoryCardNo;
        if (factory is not null && !P6TokenFactoryCatalog.IsUnitTokenFactory(factory)) return false;
        if (!state.CardObjects.TryGetValue(item.TargetObjectIds[0], out var target)) return factory is not null;
        return generation <= target.ObjectGeneration && (generation != target.ObjectGeneration
            || factory == (TokenObjectRules.IsToken(target) ? target.TokenFactoryCardNo ?? target.CardNo : null));
    }

    private static StackResolutionResult ResolveMovementDamage(MatchState state, StackItemState item, List<GameEvent> events)
    {
        if (!ValidTargetedMovementStack(state, item)) throw new InvalidOperationException("Invalid captured movement damage skill.");
        var context = item.MovementObserver!;
        var result = NoopStackResolutionResult(state);
        if (item.TargetObjectIds.SingleOrDefault() is not { } target
            || item.TargetGenerations?.GetValueOrDefault(target, -1) != state.CardObjects.GetValueOrDefault(target)?.ObjectGeneration
            || !TargetedMovementTargets(state, item).Contains(target)) return result with { Events = events };
        var spec = UnitMovedTriggerSpecRules.MovementTriggers(context.CardNo).Single(UnitMovedTriggerSpecRules.IsMovementDamageTrigger);
        var empowered = state.CardObjects.TryGetValue(item.SourceObjectId, out var source)
            && source.ObjectGeneration == context.SourceGeneration && IsFaceUpNonStandbyUnit(source)
            && IsObjectOnField(state.PlayerZones, source.ObjectId) && source.IsEmpowered;
        var amount = ApplySpellSkillDamageBonuses(state, item.ControllerId, target,
            empowered ? spec.EmpoweredDamageAmount!.Value : spec.DamageAmount!.Value);
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var locations = state.ObjectLocations.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var destroyed = new HashSet<string>(StringComparer.Ordinal);
        var damage = ApplyDamageToCardObject(state, cards, item.ControllerId, target, amount, destroyed,
            state.UntilEndOfTurnEffects.Contains(PreventSpellAndSkillDamageThisTurnEffectId), PreventSpellAndSkillDamageThisTurnEffectId);
        var payload = BuildDamagePayload(item.SourceObjectId, target, damage);
        payload["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__");
        events.Add(new("DAMAGE_APPLIED", "移动技能对目标造成伤害", payload));
        var cleanup = RunStateBasedCleanupLoop(state, zones, cards, item, state.RunePools,
            damageTriggeredDestroyTargetObjectIds: destroyed, objectLocations: locations,
            destroyedUnitOwnerIdsAlreadyThisTurn: state.DestroyedUnitOwnerIdsThisTurn);
        events.AddRange(cleanup.Events);
        return result with { PlayerZones = zones, CardObjects = cards, RunePools = cleanup.RunePools,
            TriggerQueue = cleanup.TriggerQueue, DestroyedUnitOwnerIds = cleanup.DestroyedUnitOwnerIds,
            ObjectLocations = ReconcileObjectLocations(locations, zones), Events = events };
    }
}
