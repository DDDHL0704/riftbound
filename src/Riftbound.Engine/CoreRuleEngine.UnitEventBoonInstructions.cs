using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // A cost after the mode-selection instruction is paid at resolution,
    // rather than during trigger confirmation (CN383.3.b).
    private static StackResolutionResult ResolveUnitEventBoonInstruction(MatchState state, StackItemState item,
        UnitEventAbilitySpec spec)
    {
        var context = item.UnitEvent!;
        // CN383.4.a.2: an independently captured targeted skill survives its
        // source leaving. An instruction that consumes the source's boon still
        // needs that source and the cost, as do effects that change the source.
        if (spec.Effect == "STUN_ENEMY" && !spec.ConsumesSourceBoonDuringResolution)
        {
            var selected = item.TargetObjectIds.SingleOrDefault();
            if (selected is null || !StackObjectRules.ReferencesCurrentObject(state, item, selected)
                || !UnitEventTargets(state, item).Contains(selected)) return NoopStackResolutionResult(state);
            var independentCards = state.CardObjects.ToDictionary();
            var independentEvents = new List<GameEvent>();
            ApplyUnitEventStun(state, item, NormalizeZonesForSeats(state), independentCards, independentEvents, selected);
            return NoopStackResolutionResult(state) with { CardObjects = independentCards, Events = independentEvents };
        }
        if (!state.CardObjects.TryGetValue(item.SourceObjectId, out var source)
            || source.ObjectGeneration != context.SourceGeneration || !IsFaceUpNonStandbyUnit(source)
            || !IsObjectOnField(state.PlayerZones, source.ObjectId)) return NoopStackResolutionResult(state);
        var target = item.TargetObjectIds.SingleOrDefault();
        if (spec.Effect == "STUN_ENEMY" && (target is null || !StackObjectRules.ReferencesCurrentObject(state, item, target)
            || !UnitEventTargets(state, item).Contains(target))) return NoopStackResolutionResult(state);
        if (spec.ConsumesSourceBoonDuringResolution && !source.Tags.Contains(CardObjectTags.Boon))
            return NoopStackResolutionResult(state);
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary();
        var events = new List<GameEvent>();
        var behavior = new CardBehaviorDefinition(context.CardNo, "单位触发技能", 0, item.EffectKind, 0, 0);
        using (new MightInstructionScope(state, zones, cards, events))
        {
            if (spec.ConsumesSourceBoonDuringResolution)
            {
                source = BoonRules.Consume(source);
                cards[source.ObjectId] = source;
                events.Add(CaptureBoonConsumption(new("BOON_CONSUMED", "结算时消耗来源的增益", new Dictionary<string, object?> {
                    ["playerId"] = item.ControllerId, ["sourceObjectId"] = source.ObjectId,
                    ["targetObjectId"] = source.ObjectId, ["previousPower"] = source.Power + 1, ["power"] = source.Power }), zones, cards));
            }
        }
        if (spec.Effect is "BOON_SELF" or "BOON_SELF_AND_READY")
        {
            cards[source.ObjectId] = ApplyBoon(source, behavior, item, source.ObjectId, out var granted, zones, cards);
            events.AddRange(granted);
            if (spec.Effect == "BOON_SELF_AND_READY")
            {
                cards[source.ObjectId] = ApplyReadyState(cards[source.ObjectId], behavior with { ReadiesTarget = true }, item,
                    source.ObjectId, out var readied, zones, cards);
                if (readied is not null) events.Add(readied);
            }
            if (spec.BoonsFriendlyUnitsHereIfPlayedToBattlefield && context.SourcePlayDestinationZone == "BATTLEFIELD")
                GrantFriendlyUnitBoonsAtSourceLocation(state, item, zones, cards, events);
        }
        else if (spec.Effect == "READY_SELF")
        {
            cards[source.ObjectId] = ApplyReadyState(source, behavior with { ReadiesTarget = true }, item,
                source.ObjectId, out var readied, zones, cards);
            if (readied is not null) events.Add(readied);
        }
        else if (spec.Effect == "MIGHT_SELF_AND_READY")
        {
            cards[source.ObjectId] = ApplyDirectUntilEndPowerModifier(source, source.ObjectId, item.SourceObjectId,
                context.CardNo, item.EffectKind, "UNIT_EVENT", spec.Amount, source.Power + spec.Amount);
            events.Add(new("UNIT_POWER_CHANGED", "单位在本回合内获得战力", new Dictionary<string, object?> {
                ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = source.ObjectId,
                ["powerDelta"] = spec.Amount }));
            cards[source.ObjectId] = ApplyReadyState(cards[source.ObjectId], behavior with { ReadiesTarget = true }, item,
                source.ObjectId, out var readied, zones, cards);
            if (readied is not null) events.Add(readied);
        }
        else if (target is not null) ApplyUnitEventStun(state, item, zones, cards, events, target);
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = events };
    }
    private static void ApplyUnitEventStun(MatchState state, StackItemState item,
        Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards,
        List<GameEvent> events, string target)
    {
        if (!CanExecuteStun(cards[target]) || TryReplaceEmpoweredStun(state, item, cards, target, events)) return;
        cards[target] = cards[target] with { UntilEndOfTurnEffects = AddUntilEndOfTurnEffect(cards[target].UntilEndOfTurnEffects, "STUNNED") };
        events.Add(CaptureStunObservers(new("STATUS_EFFECT_APPLIED", "眩晕目标单位", new Dictionary<string, object?> {
            ["playerId"] = item.ControllerId,
            ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, ProjectionViewers.Spectator),
            ["targetObjectId"] = target, ["effectId"] = "STUNNED", ["statusEffectId"] = "STUNNED",
            ["duration"] = "UNTIL_END_OF_TURN" }), zones, cards, item, target));
    }

}
