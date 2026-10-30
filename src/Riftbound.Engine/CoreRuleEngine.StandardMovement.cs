using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // CN 144.3: one action, one destination, possibly different origins; all
    // exhaustion costs and relocations precede triggers and state-based cleanup.
    private static ResolutionResult ResolveStandardMoveGroup(MatchState state, PlayerIntent intent, MoveUnitCommand command)
    {
        var ids = command.SourceObjectIds!.ToArray();
        if (ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length
            || (!string.IsNullOrWhiteSpace(command.SourceObjectId) && !ids.Contains(command.SourceObjectId, StringComparer.Ordinal)))
            return RejectWithCorePrompts(state, "移动单位不能包含重复或无效来源。", ErrorCodes.InvalidTarget);

        var destination = NormalizeMoveUnitLocation(command.Destination);
        var toBase = destination == MoveUnitBaseZone;
        var destinationBattlefieldId = toBase ? null : PreciseBattlefieldLocationObjectId(destination);
        if (!toBase && (string.IsNullOrWhiteSpace(destinationBattlefieldId)
            || !state.BattlefieldStates.ContainsKey(destinationBattlefieldId)))
            return RejectWithCorePrompts(state, "请选择服务端提供的同一个移动目的地。", ErrorCodes.InvalidTarget);

        var moves = new List<(string Id, string OriginZone, string Origin)>();
        foreach (var id in ids)
        {
            var location = FindFieldObjectLocation(state.PlayerZones, id);
            if (location is null || !state.CardObjects.TryGetValue(id, out var card))
                return RejectWithCorePrompts(state, "移动单位来源不在场地。", ErrorCodes.InvalidTarget);
            var origin = location.Value.Zone;
            if (origin == MoveUnitBattlefieldZone && !toBase
                && state.ObjectLocations.TryGetValue(id, out var preciseOrigin)
                && !string.IsNullOrWhiteSpace(preciseOrigin.BattlefieldObjectId))
                origin += ":" + preciseOrigin.BattlefieldObjectId;
            var costs = command.OptionalCosts ?? [];
            if (costs.Count == 0 && origin.StartsWith("BATTLEFIELD:", StringComparison.Ordinal) && !toBase
                && HasRoamPermission(state, intent.PlayerId, id, card)) costs = [MoveUnitRoamOptionalCost];
            var validation = ResolveMoveUnit(state, intent, new MoveUnitCommand(id,
                string.IsNullOrWhiteSpace(command.Origin) ? origin : command.Origin, destination, costs));
            if (!validation.Accepted)
                return RejectWithCorePrompts(state, validation.ErrorMessage ?? "移动单位不合法。", validation.ErrorCode ?? ErrorCodes.InvalidTarget);
            moves.Add((id, location.Value.Zone, origin));
        }

        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var locations = state.ObjectLocations.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var pools = state.RunePools.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var events = new List<GameEvent>();
        var bindings = ids.ToDictionary(id => id, id => state.CardObjects[id].ObjectGeneration, StringComparer.Ordinal);
        string[]? CurrentMovers(MatchState current)
        {
            if (!toBase && BattlefieldLocalRules.Battlefield(current, destinationBattlefieldId!) is null) return null;
            foreach (var move in moves)
            {
                if (!current.CardObjects.TryGetValue(move.Id, out var unit) || unit.ObjectGeneration != bindings[move.Id]
                    || !FieldObjectTypeRules.IsVisibleUnit(unit) || unit.IsExhausted || unit.IsAttacking || unit.IsDefending
                    || !SourceObjectControlledByPlayerOrLegacyOwned(unit, intent.PlayerId)
                    || !MovementRestrictionRules.CanMove(unit, intent.PlayerId)
                    || FindFieldObjectLocation(current.PlayerZones, move.Id) is not { } origin
                    || origin.PlayerId != intent.PlayerId || origin.Zone != move.OriginZone
                    || current.ObjectLocations.GetValueOrDefault(move.Id) != state.ObjectLocations.GetValueOrDefault(move.Id)
                    || toBase && BattlefieldLocalRules.PreventsMoveToBase(current, move.Id)
                    || !toBase && move.OriginZone == MoveUnitBattlefieldZone
                        && !HasRoamPermission(current, intent.PlayerId, move.Id, unit)
                        && !(TryGetBaronNestMoveDestination(current, destination, out var nest)
                            && current.CardObjects.TryGetValue(nest, out var nestCard)
                            && SourceObjectControlledByPlayerOrLegacyOwned(nestCard, intent.PlayerId))
                    || AttachedEquipmentObjectIds(current.CardObjects, move.Id) is { Count: > 0 } equipment
                        && !CanMoveExplicitAttachedEquipmentWithHost(current.PlayerZones, current.CardObjects,
                            intent.PlayerId, unit, equipment)) return null;
            }
            return ids;
        }
        var payment = PayImposedMovementCost(state, intent.PlayerId, ids[0], destinationBattlefieldId, true,
            zones, cards, locations, pools, CurrentMovers, events);
        if (payment == ImposedMovementPaymentResult.Unfunded)
            return RejectWithCorePrompts(state, "同时移动的施加费用无法支付。", ErrorCodes.InsufficientCost);
        if (payment == ImposedMovementPaymentResult.Unsupported)
            return RejectWithCorePrompts(state, "同时移动费用超出支持范围。", ErrorCodes.UnsupportedCardBehavior);
        if (payment == ImposedMovementPaymentResult.Declined)
        {
            var declined = state with { Tick = state.Tick + 1, PlayerZones = zones, CardObjects = cards,
                ObjectLocations = ReconcileObjectLocations(locations, zones), RunePools = pools };
            var continued = AdvancePendingBattlefieldTasksAfterStateChange(declined, intent.PlayerId, state);
            events.AddRange(continued.Events);
            return new(true, null, continued.State, events, ResolutionResult.BuildSnapshots(continued.State), BuildCorePrompts(continued.State));
        }
        var beforeMove = state with { PlayerZones = zones.ToDictionary(p => p.Key, p => p.Value),
            CardObjects = cards.ToDictionary(p => p.Key, p => p.Value), ObjectLocations = locations.ToDictionary(p => p.Key, p => p.Value), RunePools = pools };
        var movementObservation = CaptureMovementBatchObservers(beforeMove, cards, moves
            .Select(move => CaptureMovedUnitFact(beforeMove, move.Id, intent.PlayerId,
                toBase ? MoveUnitBaseZone : MoveUnitBattlefieldZone, destinationBattlefieldId))
            .OfType<MovedUnitFact>().ToArray());
        movementObservation = CaptureSourceExhaustionTokens(movementObservation, zones, cards, ids.Select(id => cards[id]));
        CaptureExhaustionInstruction(zones, cards, intent.PlayerId, ids, "MOVE_GROUP:" + state.Tick, events);
        var destinationZone = toBase ? MoveUnitBaseZone : MoveUnitBattlefieldZone;
        foreach (var move in moves)
        {
            RemoveFieldObjectFromLocation(zones, intent.PlayerId, move.OriginZone, move.Id);
            AddFieldObjectToLocation(zones, intent.PlayerId, destinationZone, move.Id);
            ExhaustMoveUnitSource(cards, move.Id);
            locations[move.Id] = new(intent.PlayerId, destinationZone, destinationBattlefieldId);
            events.Add(new(toBase ? "UNIT_MOVED_TO_BASE" : "UNIT_MOVED_TO_BATTLEFIELD", "同时移动单位",
                new Dictionary<string, object?>
                {
                    ["playerId"] = intent.PlayerId, ["sourceObjectId"] = move.Id, ["targetObjectId"] = move.Id,
                    ["originZone"] = move.OriginZone, ["destinationZone"] = destinationZone,
                    ["origin"] = move.Origin, ["destination"] = destination, ["battlefieldObjectId"] = destinationBattlefieldId,
                    ["simultaneousSourceObjectIds"] = ids
                }));
            var attached = AttachedEquipmentObjectIds(cards, move.Id);
            events.AddRange(MoveAttachedEquipmentWithHost(zones, attached, intent.PlayerId, move.Id, destinationZone));
            foreach (var equipment in attached) locations[equipment] = new(intent.PlayerId, destinationZone, destinationBattlefieldId);
        }

        events.Add(movementObservation);
        var cleanup = RunStateBasedCleanupLoop(state, zones, cards,
            new StackItemState($"move-group-{state.Tick + 1}", intent.PlayerId, ids[0], "MOVE_UNIT", targetObjectIds: ids),
            pools, objectLocations: locations, destroyedUnitOwnerIdsAlreadyThisTurn: state.DestroyedUnitOwnerIdsThisTurn);
        events.AddRange(cleanup.Events);
        var next = state with
        {
            Tick = state.Tick + 1, PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(locations, zones), RunePools = cleanup.RunePools,
            PassedPriorityPlayerIds = [], TriggerQueue = state.TriggerQueue.Concat(cleanup.TriggerQueue).ToArray(),
            DestroyedUnitOwnerIdsThisTurn = MergeDestroyedUnitOwnerIds(state.DestroyedUnitOwnerIdsThisTurn, cleanup.DestroyedUnitOwnerIds)
        };
        var advance = AdvancePendingBattlefieldTasksAfterStateChange(next, intent.PlayerId, state);
        events.AddRange(advance.Events);
        return new(true, null, advance.State, events, ResolutionResult.BuildSnapshots(advance.State), BuildCorePrompts(advance.State));
    }
}
