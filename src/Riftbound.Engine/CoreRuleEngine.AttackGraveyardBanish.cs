using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string AttackGraveyardBanishKind = TriggerKinds.UnitAttackBanishGraveyardUnitAssault;

    private static int AttackBanishAssaultAmount(string? cardNo)
        => SourceUnitPlayedTriggerSpecRules.TriggersForCard(cardNo).FirstOrDefault(trigger =>
            trigger.Kind == AttackGraveyardBanishKind && trigger.Timing == TriggerTimings.UnitAttack
                && trigger.TargetScope == TriggerTargetScopes.ControlledUnitInGraveyard && trigger.BanishCount == 1
                && trigger.Optional == true && trigger.GrantedKeyword == CardCombatKeywordNames.Assault
                && trigger.Duration == TriggerDurations.UntilEndOfTurn && trigger.KeywordBonus is > 0)?.KeywordBonus ?? 0;

    private static TriggerQueueItemState[] CaptureAttackGraveyardBanishTriggers(MatchState state,
        IEnumerable<string> attackers, string battlefield, string timing, string? returnFocusPlayer = null)
    {
        var queue = new List<TriggerQueueItemState>();
        foreach (var id in attackers.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (!state.CardObjects.TryGetValue(id, out var source) || !IsFaceUpNonStandbyUnit(source)) continue;
            foreach (var instance in AttachedRuleTextRules.Instances(state, source))
            {
                if (AttackBanishAssaultAmount(instance.CardNo) <= 0) continue;
                queue.Add(new($"attack-graveyard-{state.Tick + 1}-{id}-{instance.Id}", source.ControllerId!, id,
                    FieldTriggerEffect, "BATTLE_ROLES_ESTABLISHED", timing) {
                    FieldContext = new(instance.CardNo, AttackGraveyardBanishKind, source.ObjectGeneration,
                        battlefield, returnFocusPlayer) {
                        SourceLocation = new(source.ControllerId!, "BATTLEFIELD", battlefield), Origin = instance.Origin,
                        SourceTokenFactoryCardNo = IsTokenObject(source) ? source.TokenFactoryCardNo ?? source.CardNo : null
                    }
                });
            }
        }
        return queue.ToArray();
    }

    private static string[] AttackGraveyardBanishTargets(MatchState state, string controller)
        => state.PlayerZones.TryGetValue(controller, out var zones)
            ? zones.Graveyard.Where(id => state.CardObjects.TryGetValue(id, out var card)
                && card.Tags.Contains(CardObjectTags.UnitCard)).Order(StringComparer.Ordinal).ToArray() : [];

    private static StackResolutionResult ResolveAttackGraveyardBanish(MatchState state, StackItemState item)
    {
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var events = new List<GameEvent>();
        var targetId = item.TargetObjectIds.SingleOrDefault();
        var context = item.FieldContext!;
        var sourceIsPrivate = state.ObjectLocations.GetValueOrDefault(item.SourceObjectId)?.Zone is "HAND" or "MAIN_DECK" or "RUNE_DECK";
        var publicSource = sourceIsPrivate ? "HIDDEN" : item.SourceObjectId;
        // CN 355.10.a / 359.3.e.14.b: confirm the graveyard target before responses;
        // only the actual banishment satisfies "if you do". The captured ability
        // still banishes its target when its source has left the field.
        if (targetId is not null && cards.TryGetValue(targetId, out var target)
            && item.TargetGenerations?.GetValueOrDefault(targetId, -1) == target.ObjectGeneration
            && AttackGraveyardBanishTargets(state, item.ControllerId).Contains(targetId, StringComparer.Ordinal))
        {
            var own = zones[item.ControllerId];
            zones[item.ControllerId] = own with {
                Graveyard = own.Graveyard.Where(id => id != targetId).ToArray(),
                Banished = own.Banished.Append(targetId).ToArray()
            };
            ResetCardOutsidePlay(zones, cards, targetId, target, item.ControllerId);
            events.Add(new("CARD_BANISHED", "进攻技能放逐废牌堆单位", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = publicSource,
                ["targetObjectId"] = targetId, ["fromZone"] = "GRAVEYARD", ["destinationZone"] = "BANISHED"
            }));
            if (!context.SourceCeasedToExist && cards.TryGetValue(item.SourceObjectId, out var source)
                && source.ObjectGeneration == context.SourceGeneration && IsFaceUpNonStandbyUnit(source)
                && IsObjectOnField(zones, source.ObjectId))
            {
                var amount = AttackBanishAssaultAmount(context.CardNo);
                var definition = "COMBAT_KEYWORD:强攻" + amount;
                var effect = TemporaryCombatKeywordRules.AppliedEffectId(source, definition);
                cards[source.ObjectId] = source with {
                    UntilEndOfTurnEffects = source.UntilEndOfTurnEffects.Append(effect).Order(StringComparer.Ordinal).ToArray()
                };
                events.Add(new("STATUS_EFFECT_APPLIED", "实际放逐后获得本回合强攻", new Dictionary<string, object?> {
                    ["sourceObjectId"] = publicSource, ["targetObjectId"] = source.ObjectId,
                    ["effectId"] = definition, ["duration"] = "UNTIL_END_OF_TURN"
                }));
            }
        }
        events.Add(new("TRIGGER_RESOLVED", "进攻放逐技能结算完成", new Dictionary<string, object?> {
            ["sourceObjectId"] = publicSource, ["effectKind"] = item.EffectKind
        }));
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), Events = events };
    }
}
