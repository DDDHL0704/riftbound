using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static bool HasMovementDamage(StackItemState item)
        => item.MovementObserver?.Kind is TriggerKinds.UnitMovedDamageAtOriginOrDestination or TriggerKinds.UnitMovedSourceKeywordDamage;

    // CN 359.3.f.3: the origin and destination are properties of the movement
    // that triggered the skill, even if the source subsequently moves or leaves.
    private static string[] MovementDamageTargets(MatchState state, StackItemState item)
    {
        var moved = item.MovementObserver!.Unit;
        var fields = new[] { moved.Origin, moved.Destination }
            .Where(location => location.Zone == "BATTLEFIELD" && !string.IsNullOrWhiteSpace(location.BattlefieldObjectId))
            .Select(location => location.BattlefieldObjectId!).ToHashSet(StringComparer.Ordinal);
        return state.CardObjects.Values.Where(card => IsFaceUpNonStandbyUnit(card)
            && IsObjectOnField(state.PlayerZones, card.ObjectId)
            && state.ObjectLocations.TryGetValue(card.ObjectId, out var location)
            && location.Zone == "BATTLEFIELD" && location.BattlefieldObjectId is not null
            && (item.MovementObserver.Kind == TriggerKinds.UnitMovedSourceKeywordDamage || fields.Contains(location.BattlefieldObjectId))
            && TargetProtectionRules.IsLegalActivatedSkillTarget(state, item.ControllerId, card.ObjectId))
            .Select(card => card.ObjectId).Order(StringComparer.Ordinal).ToArray();
    }

    private static PendingCardChoiceState MovementDamageChoice(MatchState state, StackItemState item)
    {
        var legal = MovementDamageTargets(state, item);
        var reason = item.MovementObserver!.Kind == TriggerKinds.UnitMovedSourceKeywordDamage
            ? "选择战场上的一名单位，或不选以放弃；确认目标和法盾费用后双方可以响应。"
            : "选择移动起点或终点战场的一名单位，或不选以放弃；确认目标和法盾费用后双方可以响应。";
        return new("MOVEMENT-DAMAGE:" + item.StackItemId, "TRIGGER_CONFIRMATION", item.ControllerId,
            0, 1, legal, legal, reason, item.SourceObjectId, item.EffectKind) { ResolvingStackItemId = item.StackItemId };
    }

    private static ResolutionResult PrepareMovementDamage(ResolutionResult result, StackItemState item)
    {
        var choice = MovementDamageChoice(result.State, item);
        if (choice.LegalObjectIds.Count == 0) return PrepareTriggerConfirmation(DiscardUnconfirmedTrigger(result, item));
        var state = result.State with { PendingCardChoice = choice, PriorityPlayerId = null, ActivePlayerId = item.ControllerId };
        return result with { State = state, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static bool ValidMovementDamageChoice(MatchState state, PendingCardChoiceState choice)
        => state.StackItems.FirstOrDefault(item => item.StackItemId == choice.ResolvingStackItemId) is { TargetGenerations: null } item
            && HasMovementDamage(item) && ValidMovementDamageStack(state, item)
            && MovementDamageTargets(state, item).Length > 0 && SameDistributionChoice(choice, MovementDamageChoice(state, item));

    internal static bool ValidMovementDamageStack(MatchState state, StackItemState item)
    {
        if (!HasMovementDamage(item)) return true;
        var context = item.MovementObserver!;
        if (!ValidMovementObserver(state, context, item.EffectKind, item.ControllerId, item.SourceObjectId, item.CardNo)
            || item.SourceConfirmed || item.EffectRepeatCount != 1 || item.OptionalCosts.Count != 0
            || item.DamageAmount != 0 || item.Destination.Length != 0) return false;
        if (item.TargetGenerations is null) return item.TargetObjectIds.Count == 0 && context.TargetTokenFactoryCardNo is null;
        if (item.TargetObjectIds.Count != 1 || item.TargetGenerations.Count != 1
            || string.IsNullOrWhiteSpace(item.TargetObjectIds[0])
            || !item.TargetGenerations.TryGetValue(item.TargetObjectIds[0], out var generation) || generation < 0) return false;
        var factory = context.TargetTokenFactoryCardNo;
        if (factory is not null && !P6TokenFactoryCatalog.IsUnitTokenFactory(factory)) return false;
        if (!state.CardObjects.TryGetValue(item.TargetObjectIds[0], out var target)) return factory is not null;
        return generation <= target.ObjectGeneration && (generation != target.ObjectGeneration
            || factory == (TokenObjectRules.IsToken(target) ? target.TokenFactoryCardNo ?? target.CardNo : null));
    }

    private static StackResolutionResult ResolveMovementDamage(MatchState state, StackItemState item, List<GameEvent> events)
    {
        if (!ValidMovementDamageStack(state, item)) throw new InvalidOperationException("Invalid captured movement damage skill.");
        var context = item.MovementObserver!;
        var result = NoopStackResolutionResult(state);
        if (item.TargetObjectIds.SingleOrDefault() is not { } target
            || item.TargetGenerations?.GetValueOrDefault(target, -1) != state.CardObjects.GetValueOrDefault(target)?.ObjectGeneration
            || !MovementDamageTargets(state, item).Contains(target)) return result with { Events = events };
        var spec = UnitMovedTriggerSpecRules.MovementTriggers(context.CardNo).Single(UnitMovedTriggerSpecRules.IsMovementDamageTrigger);
        var empowered = state.CardObjects.TryGetValue(item.SourceObjectId, out var source)
            && source.ObjectGeneration == context.SourceGeneration && IsFaceUpNonStandbyUnit(source)
            && IsObjectOnField(state.PlayerZones, source.ObjectId) && source.IsEmpowered;
        // CN359.3.e.12: a departed incarnation no longer supplies its keyword value.
        var baseAmount = spec.DamageSourceKeyword is { } keyword
            ? source is not null && source.ObjectGeneration == context.SourceGeneration && IsFaceUpNonStandbyUnit(source)
                && IsObjectOnField(state.PlayerZones, source.ObjectId)
                ? ResolveCurrentCountedCombatKeyword(state, source, keyword) : 0
            : empowered ? spec.EmpoweredDamageAmount!.Value : spec.DamageAmount!.Value;
        if (baseAmount <= 0) return result with { Events = events };
        var amount = ApplySpellSkillDamageBonuses(state, item.ControllerId, target, baseAmount);
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
