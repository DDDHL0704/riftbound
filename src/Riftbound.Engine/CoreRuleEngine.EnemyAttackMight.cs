using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static TriggerSpec? EnemyAttackMightSpec(string? cardNo) => SourceUnitPlayedTriggerSpecRules.TriggersForCard(cardNo)
        .FirstOrDefault(t => t.Kind == TriggerKinds.EnemyAttackPowerModifier
            && t.Timing == TriggerTimings.EnemyAttacksControlledBattlefield && t.TargetScope == TriggerTargetScopes.TriggeringAttacker
            && t.Duration == "UNTIL_END_OF_TURN" && t.PowerDelta is < 0 && t.MinimumPowerAfterModifier is > 0);

    private static TriggerQueueItemState[] CaptureEnemyAttackMightTriggers(MatchState state, IEnumerable<string> attackers,
        string battlefieldId, string timing, string? returnFocusPlayer = null)
    {
        if (BattlefieldLocalRules.Battlefield(state, battlefieldId)?.ControllerId is not { } controller
            || !state.PlayerZones.TryGetValue(controller, out var zones)) return [];
        var queue = new List<TriggerQueueItemState>();
        foreach (var sourceId in zones.LegendZone.Order(StringComparer.Ordinal)) {
            if (!state.CardObjects.TryGetValue(sourceId, out var source) || source.IsFaceDown
                || source.ControllerId != controller || EnemyAttackMightSpec(source.CardNo) is null) continue;
            foreach (var id in attackers.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)) {
                if (!state.CardObjects.TryGetValue(id, out var attacker) || !IsFaceUpNonStandbyUnit(attacker)
                    || attacker.ControllerId == controller
                    || !IsObjectLocatedAtBattlefield(state.PlayerZones, state.ObjectLocations, id, battlefieldId)) continue;
                queue.Add(new($"enemy-attack-might-{state.Tick + 1}-{sourceId}-{id}", controller, sourceId,
                    FieldTriggerEffect, "BATTLE_ROLES_ESTABLISHED", timing) {
                    FieldContext = new(source.CardNo!, "ENEMY_ATTACK_MIGHT", source.ObjectGeneration, battlefieldId, returnFocusPlayer) {
                        AffectedUnit = new(id, attacker.ObjectGeneration) } });
            }
        }
        return queue.ToArray();
    }

    private static bool ValidEnemyAttackMightState(MatchState state, FieldTriggerContext context, string sourceId, string controller)
        => state.Seats.ContainsKey(controller) && state.CardObjects.TryGetValue(sourceId, out var source)
            && source.ObjectGeneration >= context.SourceGeneration
            && (source.ObjectGeneration != context.SourceGeneration || source.CardNo == context.CardNo && source.ControllerId == controller)
            && BattlefieldLocalRules.Battlefield(state, context.BattlefieldId) is not null
            && (context.ReturnFocusPlayerId is null || state.Seats.ContainsKey(context.ReturnFocusPlayerId))
            && context.AffectedUnit is { } affected && affected.ObjectId != sourceId
            && (context.AffectedUnitCeasedToExist ? !state.CardObjects.ContainsKey(affected.ObjectId)
                : state.CardObjects.TryGetValue(affected.ObjectId, out var unit) && unit.ObjectGeneration >= affected.Generation
                    && (unit.ObjectGeneration != affected.Generation || unit.Tags.Contains(CardObjectTags.UnitCard)));

    private static StackResolutionResult ResolveEnemyAttackMight(MatchState state, StackItemState item)
    {
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var events = new List<GameEvent>();
        var affected = item.FieldContext!.AffectedUnit!;
        // "It" is an automatic reference to the original attacker, not a chosen
        // target. Movement within the field keeps that object; leaving it does not.
        if (!item.FieldContext.AffectedUnitCeasedToExist && cards.TryGetValue(affected.ObjectId, out var unit) && unit.ObjectGeneration == affected.Generation
            && IsFaceUpNonStandbyUnit(unit) && IsObjectOnField(state.PlayerZones, affected.ObjectId)) {
            var spec = EnemyAttackMightSpec(item.CardNo)!;
            var behavior = new CardBehaviorDefinition(item.CardNo, "敌方进攻触发", 0, item.EffectKind, 0, 0,
                PowerModifierAmount: spec.PowerDelta!.Value, MinimumPowerAfterModifier: spec.MinimumPowerAfterModifier!.Value);
            cards[affected.ObjectId] = ApplyPowerModifier(state, unit, behavior, item, affected.ObjectId, spec.PowerDelta.Value, out var modified);
            events.Add(modified);
        }
        events.Add(new("TRIGGER_RESOLVED", "敌方进攻触发结算完成", new Dictionary<string, object?> { ["sourceObjectId"] = item.SourceObjectId }));
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = events };
    }
}
