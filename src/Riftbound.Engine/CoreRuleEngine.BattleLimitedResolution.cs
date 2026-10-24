using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static StackResolutionResult ResolveBattleLimitedRoleSkill(MatchState state, StackItemState item)
    {
        var context = item.FieldContext!;
        var scope = BattleLimitedMarkers.ScopeFor(state, context.BattlefieldId!);
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var events = new List<GameEvent>();
        if (scope is not null && ValidBattleLimitedContextState(state, context, item.SourceObjectId, item.ControllerId,
                item.EffectKind, item.CardNo)) {
            var self = context.Kind is BattleLimitedMarkers.DoubleAttack or BattleLimitedMarkers.DoubleDefend;
            var targetId = self ? item.SourceObjectId : item.TargetObjectIds.SingleOrDefault();
            if (targetId is not null && cards.TryGetValue(targetId, out var target)
                && IsFaceUpNonStandbyUnit(target) && IsObjectOnField(zones, targetId)
                && (self ? target.ObjectGeneration == context.SourceGeneration
                    : item.TargetGenerations?.GetValueOrDefault(targetId, -1) == target.ObjectGeneration
                        && FieldTriggerTargets(state, item).Contains(targetId))) {
                using var instruction = new MightInstructionScope(state, zones, cards, events);
                string? keywordId = null; string? powerId = null; int? snapshot = null;
                if (self) {
                    var evaluated = RecomputeRuleDerivedKeywords(state);
                    var snapshotTarget = evaluated.CardObjects.GetValueOrDefault(targetId) ?? target;
                    var beforeConditionalAuras = CurrentPowerBeforeConditionalAuras(evaluated, snapshotTarget);
                    snapshot = beforeConditionalAuras + ConditionalPowerAuraContributions(
                        evaluated, snapshotTarget, beforeConditionalAuras).Sum(contribution => contribution.Delta);
                    if (snapshot.Value > 0) {
                        var changed = ApplyDirectUntilEndPowerModifier(target, targetId, item.SourceObjectId, context.CardNo,
                            context.Kind, BattleLimitedMarkers.SourcePath, snapshot.Value, checked(target.Power + snapshot.Value));
                        var modifier = changed.UntilEndOfTurnPowerModifiers[^1];
                        powerId = BattleLimitedMarkers.PowerId(new(scope, item.StackItemId, targetId,
                            target.ObjectGeneration, modifier.AppliedOrder!.Value));
                        cards[targetId] = changed with { UntilEndOfTurnPowerModifiers = changed.UntilEndOfTurnPowerModifiers
                            .Take(changed.UntilEndOfTurnPowerModifiers.Count - 1)
                            .Append(modifier with { EffectId = powerId, Duration = BattleLimitedMarkers.Duration,
                                SnapshotMight = snapshot }).ToArray() };
                    }
                    events.Add(new("UNIT_POWER_CHANGED", "菲奥娜在本场战斗中战力翻倍", new Dictionary<string, object?> {
                        ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = targetId,
                        ["powerDelta"] = Math.Max(0, snapshot.Value), ["snapshotMight"] = snapshot.Value,
                        ["duration"] = BattleLimitedMarkers.Duration, ["stackItemId"] = item.StackItemId }));
                }
                else {
                    keywordId = TemporaryCombatKeywordRules.AppliedEffectId(target, "COMBAT_KEYWORD:坚守2");
                    cards[targetId] = target with { UntilEndOfTurnEffects = target.UntilEndOfTurnEffects.Append(keywordId).ToArray() };
                    events.Add(new("BATTLEFIELD_TRIGGER_RESOLVED", "强化阵地使一名单位在本次战斗期间获得坚守2", new Dictionary<string, object?> {
                        ["playerId"] = item.ControllerId, ["battlefieldId"] = scope.BattlefieldId,
                        ["battlefieldObjectId"] = item.SourceObjectId, ["battlefieldCardNo"] = context.CardNo,
                        ["trigger"] = TriggerKinds.BattlefieldDefendGrantSteadfast, ["targetObjectId"] = targetId,
                        ["keyword"] = CardCombatKeywordNames.Steadfast, ["keywordBonus"] = 2, ["duration"] = BattleLimitedMarkers.Duration,
                        ["stackItemId"] = item.StackItemId }));
                }
                if (keywordId is not null || powerId is not null) {
                    var grant = new BattleLimitedGrant(scope, item.SourceObjectId, context.SourceGeneration, item.ControllerId,
                        context, item.StackItemId, targetId, target.ObjectGeneration, keywordId, powerId, snapshot);
                    cards[targetId] = cards[targetId] with { UntilEndOfTurnEffects = cards[targetId].UntilEndOfTurnEffects
                        .Append(BattleLimitedMarkers.GrantId(grant)).ToArray() };
                }
            }
        }
        events.Add(new("TRIGGER_RESOLVED", "触发技能结算完成", new Dictionary<string, object?> { ["sourceObjectId"] = item.SourceObjectId }));
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, Events = events };
    }

    // Expiry deaths and replacement movements enter the existing observer collector.
    private static ResolutionResult ExpireClosedBattleLimitedEffects(ResolutionResult result)
    {
        if (!result.Accepted || !result.Events.Any(e => e.Kind == "BATTLE_CLOSED")) return result;
        var state = result.State;
        foreach (var closed in result.Events.Where(e => e.Kind == "BATTLE_CLOSED"))
            if (closed.Payload?.GetValueOrDefault("battlefieldId") is string closedField)
                state = ExpireBattleLimitedScope(state, closedField);
        if (TryReadBattleResponseDeclarationContext(state, string.Empty, out var closing)
            // Task advancement may already have opened a fresh duel on this field.
            && closing.InitialRolesEstablished
            && result.Events.Any(e => e.Kind == "BATTLE_CLOSED"
                && Equals(e.Payload?.GetValueOrDefault("battlefieldId"), closing.BattlefieldId)))
            state = state with { UntilEndOfTurnEffects = ClearBattleResponseDeclarationContextMarkers(state.UntilEndOfTurnEffects) };
        // Result/control responses may add damage after battle cleanup.
        // Expiry can make that retained damage lethal before neutral play.
        var expiryCleanup = RunStateBasedCleanupAfterSpellDuelClosed(state);
        state = expiryCleanup.State;
        return result with { State = state, Events = result.Events.Concat(expiryCleanup.Events).ToArray(),
            Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    // CN 468.7.c expires every grant from this battle, including targets that
    // moved to another battlefield or base. Other battles and EOT grants stay.
    private static MatchState ExpireBattleLimitedScope(MatchState state, string battlefield)
    {
        if (BattleLimitedMarkers.ScopeFor(state, battlefield) is not { } scope) return state;
        var cards = state.CardObjects.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        foreach (var (id, card) in state.CardObjects) {
            var grants = card.UntilEndOfTurnEffects.Where(text => text.StartsWith(BattleLimitedMarkers.GrantPrefix, StringComparison.Ordinal))
                .Select(text => BattleLimitedMarkers.TryGrant(text, out var grant) ? (Text: text, Grant: grant!)
                    : throw new InvalidOperationException("Invalid battle-limited grant marker."))
                .Where(entry => entry.Grant.Scope == scope).ToArray();
            if (grants.Length == 0) continue;
            var removed = grants.Select(entry => entry.Text).Concat(grants.Select(entry => entry.Grant.KeywordEffectId)
                .OfType<string>()).ToHashSet(StringComparer.Ordinal);
            var powers = grants.Select(entry => entry.Grant.PowerEffectId).OfType<string>().ToHashSet(StringComparer.Ordinal);
            var delta = card.UntilEndOfTurnPowerModifiers.Where(entry => powers.Contains(entry.EffectId)).Sum(entry => entry.PowerDelta);
            cards[id] = card with { Power = card.Power - delta, UntilEndOfTurnPowerModifier = card.UntilEndOfTurnPowerModifier - delta,
                UntilEndOfTurnPowerModifiers = card.UntilEndOfTurnPowerModifiers.Where(entry => !powers.Contains(entry.EffectId)).ToArray(),
                UntilEndOfTurnEffects = card.UntilEndOfTurnEffects.Where(text => !removed.Contains(text)).ToArray() };
        }
        return state with { CardObjects = cards, UntilEndOfTurnEffects = state.UntilEndOfTurnEffects.Where(text =>
            !(BattleLimitedMarkers.TryScope(text, out var ownedScope) && ownedScope == scope)
            && !(BattleLimitedMarkers.TryRole(text, out var role) && role!.Scope == scope)).ToArray() };
    }
}
