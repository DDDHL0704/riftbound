using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string AttackWeaponDamageKind = "ATTACK_DAMAGE_PER_ATTACHED_WEAPON";

    private static TriggerSpec? AttackWeaponDamageSpec(string? cardNo)
        => SourceUnitPlayedTriggerSpecRules.TriggersForCard(cardNo).FirstOrDefault(spec =>
            spec.Kind == TriggerKinds.UnitAttackDamagePerAttachedWeapon && spec.Timing == TriggerTimings.UnitAttack
            && spec.TargetScope == TriggerTargetScopes.EnemyUnitHere && spec.DamageAmount is > 0);

    private static TriggerQueueItemState[] CaptureAttackWeaponDamageTriggers(MatchState state,
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
                if (AttackWeaponDamageSpec(instance.CardNo) is null) continue;
                var context = new FieldTriggerContext(instance.CardNo, AttackWeaponDamageKind,
                    source.ObjectGeneration, battlefield, returnFocusPlayer) {
                    SourceLocation = new(controller, "BATTLEFIELD", battlefield), Origin = instance.Origin,
                    SourceTokenFactoryCardNo = IsTokenObject(source) ? source.TokenFactoryCardNo ?? source.CardNo : null };
                result.Add(new TriggerQueueItemState($"attack-weapon-damage-{state.Tick + 1}-{result.Count}",
                    controller, id, FieldTriggerEffect, "BATTLE_DECLARED", timing) {
                    FieldContext = context, SourceCardNo = instance.CardNo });
            }
        }
        return result.ToArray();
    }

    private static bool ValidAttackWeaponDamageShape(FieldTriggerContext context, string effect, string? cardNo)
        => effect == FieldTriggerEffect && context.Kind == AttackWeaponDamageKind && context.SourceGeneration >= 0
            && (cardNo is null || cardNo == context.CardNo) && AttackWeaponDamageSpec(context.CardNo) is not null
            && !string.IsNullOrWhiteSpace(context.BattlefieldId) && context.SourceLocation is not null
            && context.AttackConditionFrame is null && context.PaidUnitTrigger is null && context.StunObserver is null
            && context.RecycledBoon is null && context.EmpoweredTransition is null && context.AffectedUnit is null
            && !context.AffectedUnitCeasedToExist
            && (context.SourceTokenFactoryCardNo is null ? !context.SourceCeasedToExist
                : P6TokenFactoryCatalog.TryGetByCardNo(context.SourceTokenFactoryCardNo, out var token)
                    && token.Tags.Contains(CardObjectTags.UnitCard));

    private static bool IsAttackWeaponDamageTarget(MatchState state, StackItemState item, CardObjectState target)
        => EffectiveFieldControllerId(state.PlayerZones, target.ObjectId, target) != item.ControllerId
            && IsCombatMightTargetHere(state, item, target.ObjectId);

    private static StackResolutionResult ResolveAttackWeaponDamage(MatchState state, StackItemState item)
    {
        if (item.FieldContext is not { Kind: AttackWeaponDamageKind } context
            || !ValidFieldContextState(state, context, item.SourceObjectId, item.ControllerId, item.EffectKind, item.CardNo))
            throw new InvalidOperationException("Invalid captured weapon-scaled attack skill.");
        var result = NoopStackResolutionResult(state);
        var events = new List<GameEvent> { new("TRIGGER_RESOLVED", "进攻技能结算", new Dictionary<string, object?> {
            ["controllerId"] = item.ControllerId, ["effectKind"] = item.EffectKind,
            ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__") }) };
        if (item.TargetObjectIds.SingleOrDefault() is not { } target
            || item.TargetGenerations?.GetValueOrDefault(target, -1) != state.CardObjects.GetValueOrDefault(target)?.ObjectGeneration
            || !FieldTriggerTargets(state, item).Contains(target)) return result with { Events = events };
        var count = state.CardObjects.Values.Count(gear => !gear.IsFaceDown
            && gear.AttachedToObjectId == item.SourceObjectId && IsObjectOnField(state.PlayerZones, gear.ObjectId)
            && gear.Tags.Contains(CardObjectTags.EquipmentCard) && gear.Tags.Contains(CardEquipmentKeywordNames.Weapon));
        // CN 715.4: if no weapon yields a damage action, extra damage does not apply.
        if (count == 0) return result with { Events = events };
        var amount = ApplySpellSkillDamageBonuses(state, item.ControllerId, target,
            checked(count * AttackWeaponDamageSpec(context.CardNo)!.DamageAmount!.Value));
        if (amount <= 0) return result with { Events = events };
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var locations = state.ObjectLocations.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var destroyed = new HashSet<string>(StringComparer.Ordinal);
        var damage = ApplyDamageToCardObject(state, cards, item.ControllerId, target, amount, destroyed,
            state.UntilEndOfTurnEffects.Contains(PreventSpellAndSkillDamageThisTurnEffectId), PreventSpellAndSkillDamageThisTurnEffectId);
        events.Add(new("DAMAGE_APPLIED", "进攻技能按已贴附武装造成伤害", BuildDamagePayload(item.SourceObjectId, target, damage)));
        var cleanup = RunStateBasedCleanupLoop(state, zones, cards, item, state.RunePools,
            damageTriggeredDestroyTargetObjectIds: destroyed, objectLocations: locations,
            destroyedUnitOwnerIdsAlreadyThisTurn: state.DestroyedUnitOwnerIdsThisTurn);
        events.AddRange(cleanup.Events);
        return result with { PlayerZones = zones, CardObjects = cards, RunePools = cleanup.RunePools,
            TriggerQueue = cleanup.TriggerQueue, DestroyedUnitOwnerIds = cleanup.DestroyedUnitOwnerIds,
            ObjectLocations = ReconcileObjectLocations(locations, zones), Events = events };
    }
}
