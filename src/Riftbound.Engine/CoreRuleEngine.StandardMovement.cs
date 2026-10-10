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

        var moves = new List<(string Id, string OriginZone, string Origin, string[] Equipment)>();
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
            moves.Add((id, location.Value.Zone, origin, AttachedEquipmentObjectIds(state.CardObjects, id).ToArray()));
        }

        // Additional movement costs stack per opposing Patrol already at the destination.
        var patrolCount = StandardMovementCostRules.PowerPerExtraUnit(state, intent.PlayerId, destinationBattlefieldId);
        var powerCost = patrolCount * (ids.Length - 1);
        var pool = state.RunePools.TryGetValue(intent.PlayerId, out var available) ? available : RunePool.Empty;
        var noColoredCost = new Dictionary<string, int>(StringComparer.Ordinal);
        if (!CanPayPowerCost(pool, powerCost, noColoredCost))
            return RejectWithCorePrompts(state, $"同时移动需要额外支付 {powerCost} 符能。", ErrorCodes.InsufficientCost);

        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var locations = state.ObjectLocations.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var pools = state.RunePools.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var events = new List<GameEvent>();
        if (powerCost > 0)
        {
            var paid = PayPowerCost(pool, powerCost, noColoredCost);
            pools[intent.PlayerId] = new RunePool(pool.Mana, paid.AnyPower, paid.PowerByTrait);
            events.Add(new("COST_PAID", "支付同时移动的额外符能费用", new Dictionary<string, object?>
            {
                ["playerId"] = intent.PlayerId, ["reason"] = "MAGESEEKER_PATROL_MOVE_TAX_STATIC",
                ["manaCost"] = 0, ["powerCost"] = powerCost, ["sourceObjectIds"] = ids
            }));
        }
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
            events.AddRange(MoveAttachedEquipmentWithHost(zones, move.Equipment, intent.PlayerId, move.Id, destinationZone));
            foreach (var equipment in move.Equipment) locations[equipment] = new(intent.PlayerId, destinationZone, destinationBattlefieldId);
        }

        var relocated = state with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations, RunePools = pools };
        var queued = new List<TriggerQueueItemState>();
        foreach (var move in moves)
        {
            events.AddRange(ApplyBattlefieldMovedUnitPowerPlusOne(relocated, cards, intent.PlayerId, move.Id, move.OriginZone, destinationZone));
            events.AddRange(ResolveUnitMovedCreateDormantGoldTrigger(zones, cards, intent.PlayerId, move.Id, move.OriginZone, destinationZone));
            var trigger = BuildJhinMovementResourceTrigger(relocated, intent.PlayerId, move.Id, cards[move.Id], move.Origin, destination);
            if (trigger is not null) { queued.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger)); }
        }
        var cleanup = RunStateBasedCleanupLoop(state, zones, cards,
            new StackItemState($"move-group-{state.Tick + 1}", intent.PlayerId, ids[0], "MOVE_UNIT", targetObjectIds: ids),
            pools, objectLocations: locations, destroyedUnitOwnerIdsAlreadyThisTurn: state.DestroyedUnitOwnerIdsThisTurn);
        events.AddRange(cleanup.Events);
        var next = state with
        {
            Tick = state.Tick + 1, PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(locations, zones), RunePools = cleanup.RunePools,
            PassedPriorityPlayerIds = [], TriggerQueue = state.TriggerQueue.Concat(queued).Concat(cleanup.TriggerQueue).ToArray(),
            DestroyedUnitOwnerIdsThisTurn = MergeDestroyedUnitOwnerIds(state.DestroyedUnitOwnerIdsThisTurn, cleanup.DestroyedUnitOwnerIds)
        };
        var advance = AdvancePendingBattlefieldTasksAfterStateChange(next, intent.PlayerId, state);
        events.AddRange(advance.Events);
        return new(true, null, advance.State, events, ResolutionResult.BuildSnapshots(advance.State), BuildCorePrompts(advance.State));
    }
}
