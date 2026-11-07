using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static bool IsEnemySourceDeathKind(string kind)
        => kind is TriggerKinds.SourceEnemyBatchDestroyedReady or TriggerKinds.SourceBattlefieldEnemyDestroyedOnceGold;

    private static bool ValidEnemySourceDeathRelation(DeathObserverContext context, string controller)
        => !string.IsNullOrWhiteSpace(context.Destroyed.ControllerId) && context.Destroyed.ControllerId != controller
            && context.DestroyedLocation is null
            && (context.Kind == TriggerKinds.SourceEnemyBatchDestroyedReady
                ? context.SourceLocation is null && UnitDestroyedTriggerSpecRules.TryGetTrigger(context.CardNo,
                    UnitDestroyedTriggerSpecRules.IsEnemyBatchReadySourceTrigger, out _)
                : context.SourceLocation is { Zone: "BATTLEFIELD", BattlefieldObjectId.Length: > 0 } source
                    && !string.IsNullOrWhiteSpace(source.PlayerId) && UnitDestroyedTriggerSpecRules.TryGetTrigger(context.CardNo,
                        UnitDestroyedTriggerSpecRules.IsBattlefieldEnemyDestroyedOnceGoldTrigger, out _));

    private static StackResolutionResult ResolveEnemySourceDeathObserver(MatchState state, StackItemState item,
        DeathObserverContext context, TriggerSpec spec, GameEvent resolved)
    {
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var events = new List<GameEvent> { resolved };
        if (context.Kind == TriggerKinds.SourceBattlefieldEnemyDestroyedOnceGold)
        {
            // CN055/383: location and the once-per-turn limit were captured at
            // the death. Creating the token does not depend on the source now.
            CreateLegendEquipmentToken(zones, cards, item.ControllerId, item.SourceObjectId, spec.Kind,
                spec.CreatedTokenName!, new[] { CardObjectTags.EquipmentCard, spec.CreatedTokenName! }
                    .Concat(spec.CreatedTokenKeywords ?? []).Distinct(StringComparer.Ordinal).ToArray(),
                spec.CreatedTokenExhausted.GetValueOrDefault(true), events, capturedSourceCardNo: context.CardNo);
        }
        else if (cards.TryGetValue(item.SourceObjectId, out var source) && source.ObjectGeneration == context.SourceGeneration
            && !source.IsFaceDown && !source.Tags.Contains(CardObjectTags.Standby)
            && (IsObjectOnField(zones, source.ObjectId) || zones.Values.Any(zone => zone.LegendZone.Contains(source.ObjectId)))
            && source.IsExhausted && ReadyByEffectPermittedByFieldRules(zones, cards, source))
        {
            AddReadiedObservation(zones, cards, source, events);
            cards[source.ObjectId] = source with { IsExhausted = false };
            var kind = zones.Values.Any(zone => zone.LegendZone.Contains(source.ObjectId)) ? "LEGEND_READIED"
                : source.Tags.Contains(CardObjectTags.EquipmentCard) && !source.Tags.Contains(CardObjectTags.UnitCard)
                    ? "EQUIPMENT_READIED" : "UNIT_READIED";
            events.Add(CaptureSelfReady(new(kind, "敌方单位被摧毁后来源变为活跃状态", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
                ["targetObjectId"] = source.ObjectId, ["reason"] = context.Kind }),
                source, EffectivePublicStaticAuraSourceControllerId(zones, source.ObjectId, source), zones, cards));
        }
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, Events = events };
    }
}
