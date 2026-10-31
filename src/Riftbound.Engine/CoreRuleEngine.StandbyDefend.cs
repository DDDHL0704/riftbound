using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static CardBehaviorDefinition? StandbyDefendDefinition(string? cardNo)
        => cardNo is not null && CardBehaviorRegistry.TryGetByCardNo(cardNo, out var behavior)
            && (ShouldResolveDefendTriggerMainDeckCountedDamage(behavior) || IsStandbyDefendBomb(behavior)) ? behavior : null;

    private static bool IsStandbyDefendBomb(CardBehaviorDefinition behavior)
        => string.Equals(behavior.CardNo, "RAD·094/167", StringComparison.Ordinal)
            && behavior.CreatedBattlefieldEquipmentTokenCount > 0;

    private static TriggerQueueItemState[] CaptureStandbyDefendTriggers(MatchState state,
        IEnumerable<string> defenders, string battlefield, string timing, string? returnFocusPlayer = null)
    {
        var queue = new List<TriggerQueueItemState>();
        foreach (var id in defenders.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (!state.CardObjects.TryGetValue(id, out var source) || !FieldObjectTypeRules.IsVisibleUnit(source)) continue;
            foreach (var instance in AttachedRuleTextRules.Instances(state, source))
            {
                if (StandbyDefendDefinition(instance.CardNo) is null) continue;
                var context = new FieldTriggerContext(instance.CardNo,
                    IsStandbyDefendBomb(CardBehaviorRegistry.TryGetByCardNo(instance.CardNo, out var behavior) ? behavior : throw new InvalidOperationException())
                        ? "STANDBY_DEFEND_BOMBS" : "STANDBY_DEFEND", source.ObjectGeneration,
                    battlefield, returnFocusPlayer) { SourceLocation = new(source.ControllerId!, "BATTLEFIELD", battlefield), Origin = instance.Origin,
                        SourceTokenFactoryCardNo = IsTokenObject(source) ? source.TokenFactoryCardNo ?? source.CardNo : null };
                queue.Add(new($"standby-defend-{state.Tick + 1}-{id}-{instance.Id}", source.ControllerId!, id,
                    FieldTriggerEffect, "BATTLE_ROLES_ESTABLISHED", timing) { FieldContext = context });
            }
        }
        return queue.ToArray();
    }

    private static StackResolutionResult ResolveStandbyDefendTrigger(MatchState state, StackItemState item)
    {
        var definition = StandbyDefendDefinition(item.CardNo)!;
        if (IsStandbyDefendBomb(definition))
        {
            var battlefieldId = item.FieldContext?.BattlefieldId;
            if (string.IsNullOrWhiteSpace(battlefieldId)) return NoopStackResolutionResult(state);
            var bombZones = NormalizeZonesForSeats(state);
            var bombCards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            var bombEvents = new List<GameEvent>();
            var tokenItem = item with { TargetObjectIds = [battlefieldId] };
            var bombLocations = CreateBattlefieldEquipmentTokens(
                bombZones,
                bombCards,
                definition,
                tokenItem,
                bombEvents,
                state.ObjectLocations);
            return NoopStackResolutionResult(state) with
            {
                PlayerZones = bombZones,
                CardObjects = bombCards,
                ObjectLocations = ReconcileObjectLocations(bombLocations, bombZones),
                Events = bombEvents
            };
        }

        var target = item.TargetObjectIds.SingleOrDefault();
        // All targets must remain legal. Source departure does not remove a confirmed ability.
        if (target is null || !FieldTriggerTargets(state, item).Contains(target)
            || item.TargetGenerations?.GetValueOrDefault(target, -1) != state.CardObjects[target].ObjectGeneration)
            return NoopStackResolutionResult(state);
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var events = new List<GameEvent>();
        var destroyedByDamage = new HashSet<string>(StringComparer.Ordinal);
        var counted = ResolveCountedMainDeckDamage(state, zones, cards, item, definition.DisplayName,
            definition.DefendTriggerMainDeckLookCount, definition.DefendTriggerCountedTag,
            definition.DefendTriggerDamagePerCountedCard, definition.DefendTriggerRecyclesLookedMainDeckCards,
            id => FieldTriggerTargets(state, item).Contains(id), destroyedByDamage, events,
            ShouldPreventSpellOrSkillDamage(state, definition), "", state.RngCursor);
        var locations = state.ObjectLocations.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var removed = RunStateBasedCleanupLoop(state, zones, cards, item, state.RunePools,
            damageTriggeredDestroyTargetObjectIds: destroyedByDamage, objectLocations: locations,
            destroyedUnitOwnerIdsAlreadyThisTurn: state.DestroyedUnitOwnerIdsThisTurn);
        events.AddRange(removed.Events);
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(locations, zones), RunePools = removed.RunePools,
            TriggerQueue = removed.TriggerQueue,
            DestroyedUnitOwnerIds = removed.DestroyedUnitOwnerIds,
            RngCursor = counted.RngCursor, Events = events };
    }
}
