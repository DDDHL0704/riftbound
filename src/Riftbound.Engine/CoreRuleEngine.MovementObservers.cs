using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record MovedUnitFact(string ObjectId, long Generation, string ControllerId, string MovingPlayerId,
    ObjectLocationState Origin, ObjectLocationState Destination);
public sealed record MovementObserverContext(string CardNo, string Kind, long SourceGeneration, MovedUnitFact Unit);
internal sealed record CapturedMovementObserver(string SourceId, string ControllerId, MovementObserverContext Context);

public sealed partial class CoreRuleEngine
{
    internal const string MovementObserverEffect = "CAPTURED_UNIT_MOVEMENT_OBSERVER";
    private const string CapturedMovementEvent = "INTERNAL_MOVEMENT_OBSERVERS";
    private const string MovementResourceKind = "MOVEMENT_RESOURCE";

    internal static bool ValidMovementObserver(MovementObserverContext c, string effect, string controller, string source, string? cardNo = null)
        => effect == MovementObserverEffect && c.SourceGeneration >= 0 && (cardNo is null || cardNo == c.CardNo)
            && c.Unit is { Generation: >= 0 } unit && !string.IsNullOrWhiteSpace(unit.ObjectId)
            && !string.IsNullOrWhiteSpace(unit.ControllerId) && !string.IsNullOrWhiteSpace(unit.MovingPlayerId)
            && unit.Origin is not null && unit.Destination is not null && unit.Origin != unit.Destination
            && unit.Origin.PlayerId == unit.ControllerId && unit.Destination.PlayerId == unit.ControllerId
            && unit.Origin.Zone is "BASE" or "BATTLEFIELD" && unit.Destination.Zone is "BASE" or "BATTLEFIELD"
            && (c.Kind != TriggerKinds.BattlefieldUnitMovedAwayPowerModifier
                ? source == unit.ObjectId && controller == unit.ControllerId && c.SourceGeneration == unit.Generation
                    && UnitMovedTriggerSpecRules.MovementTriggers(c.CardNo).Any(t => t.Kind == c.Kind && UnitMovedTriggerSpecRules.MatchesMovement(t, unit))
                : c.Kind == TriggerKinds.BattlefieldUnitMovedAwayPowerModifier && unit.Origin.Zone == "BATTLEFIELD"
                    && source == unit.Origin.BattlefieldObjectId
                    && BattlefieldTriggerSpecRules.TryGetTrigger(c.CardNo, BattlefieldTriggerSpecRules.IsBattlefieldMovedUnitPowerModifierTrigger, out _));

    internal static string MovementObserverLabel(MovementObserverContext c)
        => OfficialCardSourceIdentityGroups.DisplayName(c.CardNo)
            + (c.Kind switch {
                TriggerKinds.UnitMovedCreateDormantGold => "：移动后打出休眠金币",
                TriggerKinds.UnitMovedDraw => "：移动后抽牌",
                TriggerKinds.UnitMovedGainExperience => "：移动后获得经验",
                TriggerKinds.UnitMovedPowerModifier => "：离开战场后本回合增加战力",
                _ => "：离开此处的单位本回合战力 +1" });

    // Call with the state at this movement, before cleanup or later instructions.
    // Moving player, unit controller and battlefield controller are distinct facts.
    private static GameEvent CaptureMovementObservers(MatchState before, string unitId, string movingPlayer,
        string destinationZone, string? destinationField = null)
    {
        var observers = new List<CapturedMovementObserver>();
        if (before.CardObjects.TryGetValue(unitId, out var unit) && unit.Tags.Contains(CardObjectTags.UnitCard)
            && !unit.IsFaceDown && !unit.Tags.Contains(CardObjectTags.Standby)
            && FindFieldObjectLocation(before.PlayerZones, unitId) is { } field)
        {
            var origin = before.ObjectLocations.GetValueOrDefault(unitId) ?? new(field.PlayerId, field.Zone);
            var destination = new ObjectLocationState(field.PlayerId, destinationZone, destinationField);
            var fact = new MovedUnitFact(unitId, unit.ObjectGeneration, field.PlayerId, movingPlayer, origin, destination);
            if (origin != destination)
            {
                foreach (var spec in UnitMovedTriggerSpecRules.MovementTriggers(unit.CardNo).Where(t => UnitMovedTriggerSpecRules.MatchesMovement(t, fact)))
                    observers.Add(new(unitId, field.PlayerId, new(unit.CardNo!, spec.Kind, unit.ObjectGeneration, fact)));
                if (P4ActivatedAbilityCatalog.IsSourceCardNoForAbilityId(P4ActivatedAbilityCatalog.JhinMoveResourceAbilityId, unit.CardNo))
                    observers.Add(new(unitId, field.PlayerId, new(unit.CardNo!, MovementResourceKind, unit.ObjectGeneration, fact)));
                if (BattlefieldLocalRules.AtUnit(before, unitId) is { } battlefield
                    && BattlefieldTriggerSpecRules.TryGetTrigger(battlefield.CardNo, BattlefieldTriggerSpecRules.IsBattlefieldMovedUnitPowerModifierTrigger, out var bar))
                {
                    // CN 190.6: uncontrolled battlefield skills are handled by the turn player.
                    var controller = before.BattlefieldStates.GetValueOrDefault(battlefield.ObjectId)?.ControllerId ?? before.TurnPlayerId;
                    observers.Add(new(battlefield.ObjectId, controller, new(battlefield.CardNo!, bar.Kind, battlefield.ObjectGeneration, fact)));
                }
            }
        }
        return new(CapturedMovementEvent, "捕获移动时的技能来源", new Dictionary<string, object?> { ["observers"] = observers.ToArray() });
    }

    private static ResolutionResult RecordMovementObservers(ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var events = new List<GameEvent>(); var queue = result.State.TriggerQueue.ToList();
        for (var index = 0; index < result.Events.Count; index++)
        {
            var ev = result.Events[index];
            if (ev.Kind != CapturedMovementEvent) { events.Add(ev); continue; }
            if (result.State.Status != MatchStatuses.InProgress) continue;
            foreach (var observer in (CapturedMovementObserver[])ev.Payload["observers"]!)
            {
                var resource = observer.Context.Kind == MovementResourceKind;
                var trigger = new TriggerQueueItemState($"movement-{result.State.Tick}-{index}-{observer.SourceId}-{observer.Context.Kind}",
                    observer.ControllerId, observer.SourceId,
                    resource ? P4ActivatedAbilityCatalog.JhinMoveResourceAbilityEffectKind : MovementObserverEffect,
                    observer.Context.Unit.Destination.Zone == "BASE" ? "UNIT_MOVED_TO_BASE" : "UNIT_MOVED_TO_BATTLEFIELD") { MovementObserver = resource ? null : observer.Context, SourceCardNo = observer.Context.CardNo };
                queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
            }
        }
        var state = result.State with { TriggerQueue = queue };
        return result with { State = state, Events = events, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static StackResolutionResult ResolveMovementObserver(MatchState state, StackItemState item)
    {
        var context = item.MovementObserver!;
        if (!ValidMovementObserver(context, item.EffectKind, item.ControllerId, item.SourceObjectId, item.CardNo))
            throw new InvalidOperationException("Invalid captured movement observer.");
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var events = new List<GameEvent> { BuildTriggerResolvedEvent(new(item.StackItemId, item.ControllerId,
            item.SourceObjectId, item.EffectKind, TriggerTimings.UnitMoved) { MovementObserver = context }) };
        var unitSpec = UnitMovedTriggerSpecRules.MovementTriggers(context.CardNo).FirstOrDefault(t => t.Kind == context.Kind);
        if (context.Kind == TriggerKinds.UnitMovedDraw)
        {
            var draw = ResolveLastBreathDrawStackItem(state, item, unitSpec!.DrawCount!.Value);
            return draw with { Events = draw.Events.Select(e => e.Kind == "TRIGGER_RESOLVED" ? events[0] : e).ToArray() };
        }
        if (context.Kind == TriggerKinds.UnitMovedGainExperience)
            return NoopStackResolutionResult(state) with { PlayerExperience = GainExperience(NormalizeExperienceForSeats(state),
                item.ControllerId, unitSpec!.ExperienceCount!.Value, item, events, item.SourceObjectId, context.CardNo), Events = events };
        if (context.Kind == TriggerKinds.UnitMovedCreateDormantGold)
        {
            UnitMovedTriggerSpecRules.TryGetTrigger(context.CardNo, UnitMovedTriggerSpecRules.IsUnitMovedCreateDormantGoldTrigger, out var spec);
            for (var i = 0; i < spec.CreatedTokenCount.GetValueOrDefault(); i++)
                CreateLegendEquipmentToken(zones, cards, item.ControllerId, item.SourceObjectId, spec.Kind,
                    spec.CreatedTokenName!, new[] { CardObjectTags.EquipmentCard, spec.CreatedTokenName! }
                        .Concat(spec.CreatedTokenKeywords ?? []).Distinct(StringComparer.Ordinal).ToArray(),
                    spec.CreatedTokenExhausted.GetValueOrDefault(true), events, capturedSourceCardNo: context.CardNo);
        }
        else if (cards.TryGetValue(context.Unit.ObjectId, out var unit) && unit.ObjectGeneration == context.Unit.Generation
            && IsObjectOnField(zones, unit.ObjectId) && unit.Tags.Contains(CardObjectTags.UnitCard))
        {
            var spec = unitSpec;
            if (spec is null) BattlefieldTriggerSpecRules.TryGetTrigger(context.CardNo, BattlefieldTriggerSpecRules.IsBattlefieldMovedUnitPowerModifierTrigger, out spec);
            var delta = spec!.PowerDelta.GetValueOrDefault();
            var behavior = new CardBehaviorDefinition(context.CardNo, OfficialCardSourceIdentityGroups.DisplayName(context.CardNo), 0, spec.Kind, 0, 0, PowerModifierAmount: delta);
            cards[unit.ObjectId] = ApplyPowerModifier(unit, behavior, item, unit.ObjectId, delta, out var powerEvent);
            events.Add(powerEvent);
        }
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), Events = events };
    }
}
