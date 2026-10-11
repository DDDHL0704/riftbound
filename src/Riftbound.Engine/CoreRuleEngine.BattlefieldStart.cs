using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record BattlefieldStartContext(string CardNo, string BattlefieldObjectId, int Damage, long SourceGeneration);

public sealed partial class CoreRuleEngine
{
    internal const string BattlefieldStartDamageEffect = "CAPTURED_BATTLEFIELD_START_DAMAGE";

    private static MatchState QueueBattlefieldStartDamage(MatchState state, List<GameEvent> events)
    {
        var queue = state.TriggerQueue.ToList();
        foreach (var id in state.PlayerZones.Values.SelectMany(z => z.Battlefields).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (!state.CardObjects.TryGetValue(id, out var source) || !IsBattlefieldCardObject(source) || source.IsFaceDown
                || !BattlefieldTriggerSpecRules.TryGetTrigger(source.CardNo,
                    BattlefieldTriggerSpecRules.IsBattlefieldTurnStartDamageAllUnitsTrigger, out var definition)) continue;
            var context = new BattlefieldStartContext(source.CardNo!, id, definition.DamageAmount!.Value, source.ObjectGeneration);
            var trigger = new TriggerQueueItemState($"start-damage-{state.TurnNumber}-{state.Tick}-{id}",
                state.TurnPlayerId, id, BattlefieldStartDamageEffect, "TURN_START_BEGAN") { BattlefieldStart = context };
            queue.Add(trigger);
            events.Add(BuildTriggerQueuedEvent(trigger));
        }
        return state with { TriggerQueue = queue };
    }

    internal static bool ValidBattlefieldStart(MatchState state, BattlefieldStartContext? context,
        string effect, string controller, string source, string? cardNo = null)
    {
        if (context is null) return effect != BattlefieldStartDamageEffect;
        return effect == BattlefieldStartDamageEffect && state.TurnStartStep == "SCORE"
            && state.Phase == MatchPhases.TurnStart && controller == state.TurnPlayerId
            && source == context.BattlefieldObjectId && context.SourceGeneration >= 0
            && (cardNo is null || cardNo == context.CardNo)
            && state.CardObjects.TryGetValue(context.BattlefieldObjectId, out var field) && IsBattlefieldCardObject(field)
            && BattlefieldTriggerSpecRules.TryGetTrigger(context.CardNo,
                BattlefieldTriggerSpecRules.IsBattlefieldTurnStartDamageAllUnitsTrigger, out var definition)
            && context.Damage == definition.DamageAmount;
    }

    private static StackResolutionResult ResolveBattlefieldStartDamage(MatchState state, StackItemState item)
    {
        var context = item.BattlefieldStart!;
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        var locations = state.ObjectLocations.ToDictionary(e => e.Key, e => e.Value);
        var targets = HeldUnitsAt(state, context.BattlefieldObjectId);
        var events = new List<GameEvent> { new("BATTLEFIELD_TRIGGER_RESOLVED", "回合开始时的战场伤害技能结算",
            new Dictionary<string, object?> { ["playerId"] = item.ControllerId,
                ["trigger"] = TriggerKinds.BattlefieldTurnStartDamageAllUnits,
                ["sourceObjectIds"] = new[] { item.SourceObjectId }, ["targetObjectIds"] = targets,
                ["damage"] = context.Damage, ["timing"] = MatchPhases.TurnStart, ["beforeScoring"] = true }) };
        var destroyed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in targets)
        {
            var damage = ApplyDamageToCardObject(cards, item.ControllerId, id, context.Damage, destroyed,
                state.UntilEndOfTurnEffects.Contains(PreventSpellAndSkillDamageThisTurnEffectId), PreventSpellAndSkillDamageThisTurnEffectId);
            var payload = BuildDamagePayload(item.SourceObjectId, id, damage);
            payload["sourceObjectIds"] = new[] { item.SourceObjectId };
            payload["reason"] = TriggerKinds.BattlefieldTurnStartDamageAllUnits;
            payload["timing"] = MatchPhases.TurnStart;
            events.Add(new("DAMAGE_APPLIED", "战场技能对结算时此处的单位造成伤害", payload));
        }
        var cleanup = RunStateBasedCleanupLoop(state, zones, cards, item, state.RunePools,
            damageTriggeredDestroyTargetObjectIds: destroyed, objectLocations: locations,
            destroyedUnitOwnerIdsAlreadyThisTurn: state.DestroyedUnitOwnerIdsThisTurn);
        events.AddRange(cleanup.Events);
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(locations, zones), RunePools = cleanup.RunePools,
            TriggerQueue = cleanup.TriggerQueue, DestroyedUnitOwnerIds = cleanup.DestroyedUnitOwnerIds, Events = events };
    }
}
