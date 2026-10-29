using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string AttackAreaDamageKind = "ATTACK_AREA_DAMAGE";

    private static TriggerSpec? AttackAreaDamageSpec(string? cardNo) => SourceUnitPlayedTriggerSpecRules.TriggersForCard(cardNo)
        .FirstOrDefault(t => t.Kind == TriggerKinds.UnitAttackDamageAllEnemyUnitsHere
            && t.Timing == TriggerTimings.UnitAttack && t.TargetScope == TriggerTargetScopes.AllEnemyUnitsHere
            && t.DamageAmount is > 0 && t.MaximumControlledRunes is null or >= 0);

    private static TriggerQueueItemState[] CaptureAttackAreaDamageTriggers(MatchState state, IEnumerable<string> attackers,
        string battlefield, string timing = TimingStates.NeutralClosed, string? returnFocusPlayer = null)
    {
        var result = new List<TriggerQueueItemState>();
        LastKnownMightFrame? frame = null;
        foreach (var id in attackers.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (!state.CardObjects.TryGetValue(id, out var source) || !IsFaceUpNonStandbyUnit(source)
                || source.ControllerId is not { Length: > 0 } controller) continue;
            foreach (var instance in AttachedRuleTextRules.Instances(state, source))
            {
                var spec = AttackAreaDamageSpec(instance.CardNo);
                if (spec is null || spec.MaximumControlledRunes is { } maximum
                    && EmpowerRules.ControlledRunes(state, controller) > maximum) continue;
                var context = new FieldTriggerContext(instance.CardNo, AttackAreaDamageKind, source.ObjectGeneration,
                    battlefield, returnFocusPlayer) {
                    SourceLocation = new(controller, "BATTLEFIELD", battlefield), Origin = instance.Origin,
                    SourceTokenFactoryCardNo = IsTokenObject(source) ? source.TokenFactoryCardNo ?? source.CardNo : null,
                    AttackConditionFrame = spec.MaximumControlledRunes is null ? null
                        : frame ??= CapturePublicMightFrame(state, state.PlayerZones, state.CardObjects)
                };
                result.Add(new TriggerQueueItemState($"attack-area-{state.Tick + 1}-{result.Count}", controller, id,
                    FieldTriggerEffect, "BATTLE_DECLARED", timing) { FieldContext = context });
            }
        }
        return result.ToArray();
    }

    private static bool ValidAttackAreaCondition(MatchState state, FieldTriggerContext context, string sourceId, string controller)
    {
        var spec = AttackAreaDamageSpec(context.CardNo);
        if (spec is null) return false;
        if (spec.MaximumControlledRunes is not { } maximum) return context.AttackConditionFrame is null;
        try
        {
            if (context.AttackConditionFrame is not { } frame || !ValidMightFrameExperience(frame)
                || frame.CardObjects is null || frame.ObjectLocations is null || frame.BattleDeclarations is null
                || !frame.PlayerZones.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(state.Seats.Keys)
                || frame.PlayerZones.Values.Any(z => z is null || z.Hand.Count != 0 || z.MainDeck.Count != 0 || z.RuneDeck.Count != 0)
                || frame.BattleDeclarations.Any(e => e is null || !e.StartsWith("BATTLE_RESPONSE_DECLARATION_CONTEXT:", StringComparison.Ordinal))
                || !frame.CardObjects.TryGetValue(sourceId, out var captured) || captured.ObjectGeneration != context.SourceGeneration
                || captured.ControllerId != controller || !IsFaceUpNonStandbyUnit(captured)
                || !frame.ObjectLocations.TryGetValue(sourceId, out var location) || location.Zone != "BATTLEFIELD"
                || location.BattlefieldObjectId != context.BattlefieldId
                || !AttachedRuleTextRules.Instances(frame.PlayerZones, frame.CardObjects, captured)
                    .Any(instance => instance.CardNo == context.CardNo && instance.Origin == context.Origin)) return false;
            var publicIds = frame.PlayerZones.Values.SelectMany(z => z.Base.Concat(z.Battlefields).Concat(z.Graveyard)
                .Concat(z.Banished).Concat(z.LegendZone).Concat(z.ChampionZone)).ToHashSet(StringComparer.Ordinal);
            if (frame.CardObjects.Any(p => p.Value is null || p.Key != p.Value.ObjectId || p.Value.IsFaceDown
                    || !publicIds.Contains(p.Key) || (state.CardObjects.TryGetValue(p.Key, out var current)
                        ? current.ObjectGeneration < p.Value.ObjectGeneration : !HasKnownHistoricalTokenFactory(p.Value)))
                || frame.ObjectLocations.Keys.Any(id => !frame.CardObjects.ContainsKey(id))) return false;
            return EmpowerRules.ControlledRunes(MightFrameState(frame), controller) <= maximum;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or KeyNotFoundException
            or NullReferenceException or OverflowException) { return false; }
    }

    private static StackResolutionResult ResolveAttackAreaDamage(MatchState state, StackItemState item)
    {
        var spec = AttackAreaDamageSpec(item.CardNo)!;
        var here = CombatMightSourceLocation(state, item.SourceObjectId, item.FieldContext!.SourceGeneration);
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var locations = state.ObjectLocations.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var sourceIsPrivate = state.ObjectLocations.TryGetValue(item.SourceObjectId, out var sourceLocation)
            && sourceLocation.Zone is "HAND" or "MAIN_DECK" or "RUNE_DECK";
        var events = new List<GameEvent> { new("TRIGGER_RESOLVED", "进攻范围伤害技能结算", new Dictionary<string, object?> {
            ["sourceObjectId"] = sourceIsPrivate ? "HIDDEN" : item.SourceObjectId,
            ["controllerId"] = item.ControllerId, ["effectKind"] = item.EffectKind }) };
        var destroyed = new HashSet<string>(StringComparer.Ordinal);
        if (here is not null)
        {
            var targets = state.CardObjects.Values.Where(card => IsFaceUpNonStandbyUnit(card)
                && card.ControllerId != item.ControllerId && IsObjectOnField(state.PlayerZones, card.ObjectId)
                && state.ObjectLocations.TryGetValue(card.ObjectId, out var location) && location.Zone == here.Zone
                && (here.Zone == "BASE" ? location.PlayerId == here.PlayerId : location.BattlefieldObjectId == here.BattlefieldObjectId))
                .Select(card => card.ObjectId).Order(StringComparer.Ordinal).ToArray();
            var amount = checked(spec.DamageAmount!.Value + DistributionBonuses(state, item.ControllerId, null).Sum(bonus => bonus.Amount));
            foreach (var id in targets)
            {
                var targetDamage = ApplyBattlefieldTargetSpellSkillDamageBonus(state, id, amount);
                var damage = ApplyDamageToCardObject(state, cards, item.ControllerId, id, targetDamage, destroyed,
                    state.UntilEndOfTurnEffects.Contains(PreventSpellAndSkillDamageThisTurnEffectId), PreventSpellAndSkillDamageThisTurnEffectId);
                events.Add(new("DAMAGE_APPLIED", "进攻技能对同处敌方单位造成伤害", BuildDamagePayload(item.SourceObjectId, id, damage)));
            }
        }
        var cleanup = RunStateBasedCleanupLoop(state, zones, cards, item, state.RunePools,
            damageTriggeredDestroyTargetObjectIds: destroyed, objectLocations: locations,
            destroyedUnitOwnerIdsAlreadyThisTurn: state.DestroyedUnitOwnerIdsThisTurn);
        events.AddRange(cleanup.Events);
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, Events = events,
            RunePools = cleanup.RunePools, TriggerQueue = cleanup.TriggerQueue,
            DestroyedUnitOwnerIds = cleanup.DestroyedUnitOwnerIds, ObjectLocations = ReconcileObjectLocations(locations, zones) };
    }
}
