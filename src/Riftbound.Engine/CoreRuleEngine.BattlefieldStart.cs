using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record BattlefieldStartContext(string CardNo, string BattlefieldObjectId, int Damage, long SourceGeneration,
    int DrawCount = 0, string? ControllerId = null);

public sealed partial class CoreRuleEngine
{
    internal const string BattlefieldStartDamageEffect = "CAPTURED_BATTLEFIELD_START_DAMAGE";

    internal const string BattlefieldStartDrawEffect = "CAPTURED_BATTLEFIELD_START_DESTROY_DRAW";

    private static MatchState QueueBattlefieldStartAbilities(MatchState state, List<GameEvent> events)
    {
        var queue = state.TriggerQueue.ToList();
        foreach (var id in state.PlayerZones.Values.SelectMany(z => z.Battlefields).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (!state.CardObjects.TryGetValue(id, out var source) || !IsBattlefieldCardObject(source) || source.IsFaceDown) continue;
            var damage = BattlefieldTriggerSpecRules.TryGetTrigger(source.CardNo,
                BattlefieldTriggerSpecRules.IsBattlefieldTurnStartDamageAllUnitsTrigger, out var damageSpec) ? damageSpec.DamageAmount!.Value : 0;
            var draw = source.ControllerId == state.TurnPlayerId && BattlefieldTriggerSpecRules.TryGetTrigger(source.CardNo,
                BattlefieldTriggerSpecRules.IsBattlefieldTurnStartDestroyUnitDrawTrigger, out var drawSpec)
                && drawSpec.DestroyCount == 1 ? drawSpec.DrawCount!.Value : 0;
            if (damage == 0 && draw == 0) continue;
            var effect = draw > 0 ? BattlefieldStartDrawEffect : BattlefieldStartDamageEffect;
            var controller = source.ControllerId ?? state.TurnPlayerId;
            var context = new BattlefieldStartContext(source.CardNo!, id, damage, source.ObjectGeneration, draw, controller);
            if (!ValidBattlefieldStart(state, context, effect, controller, id)) continue;
            var trigger = new TriggerQueueItemState($"start-{state.TurnNumber}-{state.Tick}-{id}",
                controller, id, effect, "TURN_START_BEGAN") { BattlefieldStart = context };
            queue.Add(trigger);
            events.Add(BuildTriggerQueuedEvent(trigger));
        }
        return state with { TriggerQueue = queue };
    }

    internal static bool ValidBattlefieldStartDefinition(BattlefieldStartContext context, string effect, string? cardNo = null)
        => context.SourceGeneration >= 0 && !string.IsNullOrWhiteSpace(context.BattlefieldObjectId)
            && (cardNo is null || cardNo == context.CardNo)
            && (effect == BattlefieldStartDamageEffect && context.DrawCount == 0
                && BattlefieldTriggerSpecRules.TryGetTrigger(context.CardNo, BattlefieldTriggerSpecRules.IsBattlefieldTurnStartDamageAllUnitsTrigger, out var damage)
                && context.Damage == damage.DamageAmount
                || effect == BattlefieldStartDrawEffect && context.Damage == 0
                && BattlefieldTriggerSpecRules.TryGetTrigger(context.CardNo, BattlefieldTriggerSpecRules.IsBattlefieldTurnStartDestroyUnitDrawTrigger, out var draw)
                && draw.DestroyCount == 1 && context.DrawCount == draw.DrawCount);

    internal static bool ValidBattlefieldStart(MatchState state, BattlefieldStartContext? context,
        string effect, string controller, string source, string? cardNo = null)
    {
        if (context is null) return effect is not (BattlefieldStartDamageEffect or BattlefieldStartDrawEffect);
        return ValidBattlefieldStartDefinition(context, effect, cardNo) && state.TurnStartStep == "SCORE"
            && state.Phase == MatchPhases.TurnStart && state.Seats.ContainsKey(controller)
            && context.ControllerId == controller
            && source == context.BattlefieldObjectId
            && state.CardObjects.TryGetValue(context.BattlefieldObjectId, out var field) && IsBattlefieldCardObject(field);
    }

    private static StackResolutionResult ResolveBattlefieldStart(MatchState state, StackItemState item)
    {
        var context = item.BattlefieldStart!;
        if (context.DrawCount > 0) return ResolveBattlefieldStartDraw(state, item);
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
    private static StackResolutionResult ResolveBattlefieldStartDraw(MatchState state, StackItemState item)
    {
        if (item.TriggerCost?.DestructionTarget is null) return NoopStackResolutionResult(state);
        var zones = NormalizeZonesForSeats(state);
        var events = new List<GameEvent> { new("BATTLEFIELD_TRIGGER_RESOLVED", "已支付摧毁费用的开始技能抽牌",
            new Dictionary<string, object?> { ["playerId"] = item.ControllerId,
                ["trigger"] = TriggerKinds.BattlefieldTurnStartDestroyUnitDraw, ["sourceObjectIds"] = new[] { item.SourceObjectId },
                ["targetObjectId"] = item.TriggerCost.DestructionTarget.ObjectId, ["drawCount"] = item.BattlefieldStart!.DrawCount,
                ["timing"] = MatchPhases.TurnStart, ["beforeScoring"] = true }) };
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var draw = ApplyDrawToPlayer(state, zones, cards, NormalizeScoresForSeats(state), item.ControllerId,
            item.BattlefieldStart!.DrawCount, state.RngCursor, events,
            deferNormalVictory: true);
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, PlayerScores = draw.PlayerScores,
            WinnerPlayerId = draw.WinnerPlayerId, RngCursor = draw.RngCursor, Events = events,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
    }

}
