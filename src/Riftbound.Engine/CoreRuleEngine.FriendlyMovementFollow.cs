using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static bool ValidFriendlyMovementFollowShape(MovementObserverContext context, string controller, string source)
        => UnitMovedTriggerSpecRules.MovementTriggers(context.CardNo).Any(UnitMovedTriggerSpecRules.IsFriendlyMovementFollowTrigger)
            && context.Unit.ObjectId != source && context.Unit.ControllerId == controller
            && context.SourceLocation == context.Unit.Origin
            && PreciseFollowLocation(context.Unit.Origin) && PreciseFollowLocation(context.Unit.Destination)
            && context.SourceFaceRevision is >= 0 && context.CapturedTick is > 0
            && context.SourceWasEmpowered is null && context.TargetTokenFactoryCardNo is null
            && context.MovementBatch is null && context.MovementOrdinal is null
            && context.ControlledRunesAtTrigger is null && context.Mode is null && context.SelectedPlayerId is null;

    private static bool PreciseFollowLocation(ObjectLocationState at)
        => at.Zone == "BASE" ? at.BattlefieldObjectId is null
            : at.Zone == "BATTLEFIELD" && !string.IsNullOrWhiteSpace(at.BattlefieldObjectId);

    private static bool ValidFriendlyMovementFollowSource(MatchState state, MovementObserverContext context, string source)
        => context.CapturedTick <= state.Tick && (context.Origin is not null
            || PredictBindingMatches(state, new(source, context.SourceGeneration), context.CardNo,
                context.SourceFaceRevision!.Value, context.CapturedTick!.Value, null));

    private static IEnumerable<CapturedMovementObserver> CaptureFriendlyMovementFollowObservers(MatchState state,
        IReadOnlyList<MovedUnitFact> units)
    {
        foreach (var source in state.CardObjects.Values.Where(source => FieldObjectTypeRules.IsVisibleUnit(source)
            && IsObjectOnField(state.PlayerZones, source.ObjectId)))
        {
            var controller = EffectiveFieldControllerId(state.PlayerZones, source.ObjectId, source);
            var here = units.FirstOrDefault(unit => unit.ObjectId == source.ObjectId)?.Destination
                ?? (TryGetPreciseFieldLocation(state.PlayerZones, state.ObjectLocations, source.ObjectId, out var at) ? at : null);
            foreach (var instance in AttachedRuleTextRules.Instances(state, source))
            foreach (var spec in UnitMovedTriggerSpecRules.MovementTriggers(instance.CardNo)
                .Where(UnitMovedTriggerSpecRules.IsFriendlyMovementFollowTrigger))
            foreach (var unit in units.Where(unit => unit.ObjectId != source.ObjectId
                && unit.ControllerId == controller && unit.Origin == here))
                yield return new(source.ObjectId, controller, new(instance.CardNo, spec.Kind,
                    source.ObjectGeneration, unit, SourceLocation: here) {
                    RuleTextInstanceId = instance.Origin is null ? null : instance.Id, Origin = instance.Origin,
                    SourceFaceRevision = instance.Origin?.FaceRevision ?? source.CopyFaceTransitions?.Count ?? 0,
                    CapturedTick = RuleChoices.Value?.CompletionTick ?? state.Tick + 1 });
        }
    }

    private static StackResolutionResult ResolveFriendlyMovementFollow(MatchState state, StackItemState item)
    {
        var context = item.MovementObserver!;
        var resolved = BuildTriggerResolvedEvent(new(item.StackItemId, item.ControllerId,
            item.SourceObjectId, item.EffectKind, TriggerTimings.UnitMoved) { MovementObserver = context });
        var events = new List<GameEvent> { resolved with { Payload = new Dictionary<string, object?>(resolved.Payload) {
            ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, ProjectionViewers.Spectator) } } };
        var result = NoopStackResolutionResult(state);
        if (!state.CardObjects.TryGetValue(item.SourceObjectId, out var source)
            || source.ObjectGeneration != context.SourceGeneration || !FieldObjectTypeRules.IsVisibleUnit(source)
            || !IsObjectOnField(state.PlayerZones, source.ObjectId)) return result with { Events = events };
        var controller = EffectiveFieldControllerId(state.PlayerZones, source.ObjectId, source);
        var destination = context.Unit.Destination with { PlayerId = controller };
        if (!TryGetPreciseFieldLocation(state.PlayerZones, state.ObjectLocations, source.ObjectId, out var here)
            || here == destination
            || destination.Zone == "BATTLEFIELD" && BattlefieldLocalRules.Battlefield(state, destination.BattlefieldObjectId) is null)
            return result with { Events = events };
        if (RuleChoices.Value!.Choose(item.ControllerId, "是否跟随刚从同一位置移走的友方单位？",
            [new("FOLLOW", "跟随至本次移动的终点"), new("DECLINE", "放弃")],
            kind: "FRIENDLY_MOVEMENT_FOLLOW", preview: state) != "FOLLOW") return result with { Events = events };
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary();
        var locations = state.ObjectLocations.ToDictionary(); var pools = state.RunePools.ToDictionary();
        TryMoveFieldUnitByEffect(state, zones, cards, locations, pools, source.ObjectId, destination,
            item.ControllerId, item, events);
        return result with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations, RunePools = pools, Events = events };
    }
}
