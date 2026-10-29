using Riftbound.Contracts;
namespace Riftbound.Engine;

public sealed record ReflexiveDuelContext(string CardNo, ObjectBinding MovedUnit, ObjectLocationState? Destination);

public sealed partial class CoreRuleEngine
{
    internal const string ReflexiveDuelEffect = "REFLEXIVE_MOVEMENT_DUEL";
    internal static bool ValidReflexiveDuel(ReflexiveDuelContext c, string effect, string? no = null)
        => effect == ReflexiveDuelEffect && (no is null || no == c.CardNo)
            && CardBehaviorRegistry.TryGetByCardNo(c.CardNo, out var b) && b.QueuesDestinationDuel
            && c.MovedUnit is { Generation: >= 0 } && !string.IsNullOrWhiteSpace(c.MovedUnit.ObjectId)
            && (c.Destination is null || !string.IsNullOrWhiteSpace(c.Destination.PlayerId)
                && (c.Destination.Zone == "BASE" && c.Destination.BattlefieldObjectId is null
                    || c.Destination.Zone == "BATTLEFIELD" && !string.IsNullOrWhiteSpace(c.Destination.BattlefieldObjectId)));
    internal static bool ValidReflexiveDuelStack(MatchState state, StackItemState item)
    {
        if (item.ReflexiveDuel is not { } c) return item.EffectKind != ReflexiveDuelEffect;
        if (!ValidReflexiveDuel(c, item.EffectKind, item.CardNo)) return false;
        return item.TargetGenerations is { } g
            ? item.TargetObjectIds.Count == 1 && item.TargetObjectIds[0] != c.MovedUnit.ObjectId
                && g.Count == 1 && g.TryGetValue(item.TargetObjectIds[0], out var generation) && generation >= 0
            : item.TargetObjectIds.Count == 0 && state.PriorityPlayerId is null
                && (state.PendingCardChoice is not null || state.TriggerQueue.Count > 0);
    }
    private static void CaptureDestinationDuel(MatchState before, StackItemState item,
        IReadOnlyDictionary<string, CardObjectState> cards, IReadOnlyDictionary<string, ObjectLocationState> locations, List<GameEvent> events)
    {
        var id = item.TargetObjectIds.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(id) || !before.CardObjects.TryGetValue(id, out var previous)) return;
        // No condition is printed for the embedded trigger (CN387.2). A failed
        // move has no actual destination, so its trigger has no confirmable target.
        var moved = cards.TryGetValue(id, out var current) && current.MovementCountThisTurn > previous.MovementCountThisTurn;
        var context = new ReflexiveDuelContext(item.CardNo!, new(id, item.TargetGenerations?.GetValueOrDefault(id, previous.ObjectGeneration) ?? previous.ObjectGeneration),
            moved ? locations.GetValueOrDefault(id) : null);
        events.Add(new("REFLEXIVE_DUEL_CREATED", "移动指示已处理，互斗技能等待确认", new Dictionary<string, object?> {
            ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId, ["reflexiveDuel"] = context }));
    }
    private static ResolutionResult QueueReflexiveDuels(ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var queue = result.State.TriggerQueue.ToList(); var events = result.Events.ToList();
        foreach (var (ev, index) in result.Events.Select((e, i) => (e, i)))
        {
            if (ev.Kind != "REFLEXIVE_DUEL_CREATED" || ev.Payload.GetValueOrDefault("reflexiveDuel") is not ReflexiveDuelContext c) continue;
            var trigger = new TriggerQueueItemState($"reflexive-duel-{result.State.Tick}-{index}",
                (string)ev.Payload["playerId"]!, (string)ev.Payload["sourceObjectId"]!, ReflexiveDuelEffect, "MOVEMENT_INSTRUCTION_COMPLETED") { ReflexiveDuel = c };
            queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
        }
        if (queue.Count == result.State.TriggerQueue.Count) return result;
        var state = result.State with { TriggerQueue = queue };
        return result with { State = state, Events = events, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }
    private static string[] ReflexiveDuelTargets(MatchState state, StackItemState item)
    {
        var context = item.ReflexiveDuel!;
        if (context.Destination is not { } destination) return [];
        return state.CardObjects.Values.Where(c => c.ObjectId != context.MovedUnit.ObjectId && FieldObjectTypeRules.IsVisibleUnit(c)
                && FindFieldObjectLocation(state.PlayerZones, c.ObjectId) is { } field && field.PlayerId != item.ControllerId
                && state.ObjectLocations.TryGetValue(c.ObjectId, out var p) && p.Zone == destination.Zone
                && (p.Zone == "BASE" ? p.PlayerId == destination.PlayerId : p.BattlefieldObjectId == destination.BattlefieldObjectId)
                && TargetProtectionRules.IsLegalActivatedSkillTarget(state, item.ControllerId, c.ObjectId))
            .Select(c => c.ObjectId).Order(StringComparer.Ordinal).ToArray();
    }
    private static PendingCardChoiceState ReflexiveDuelChoice(MatchState state, StackItemState item)
        => new("REFLEXIVE-DUEL-TARGET:" + item.StackItemId, "TRIGGER_CONFIRMATION", item.ControllerId, 1, 1,
            ReflexiveDuelTargets(state, item), [item.ReflexiveDuel!.MovedUnit.ObjectId],
            "选择移动终点的另一名敌方单位。支付适用法盾费用后双方可响应，随后两名单位互斗。", item.SourceObjectId, item.EffectKind)
            { ResolvingStackItemId = item.StackItemId };
    private static ResolutionResult PrepareReflexiveDuelTarget(ResolutionResult result, StackItemState item)
    {
        var choice = ReflexiveDuelChoice(result.State, item);
        if (choice.LegalObjectIds.Count == 0) return PrepareTriggerConfirmation(DiscardUnconfirmedTrigger(result, item));
        var state = result.State with { PendingCardChoice = choice, PriorityPlayerId = null, ActivePlayerId = item.ControllerId };
        return result with { State = state, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }
    private static bool ValidReflexiveDuelChoice(MatchState state, PendingCardChoiceState choice)
    {
        var item = state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId);
        if (item?.ReflexiveDuel is null || item.TargetGenerations is not null || !ValidReflexiveDuel(item.ReflexiveDuel, item.EffectKind, item.CardNo)) return false;
        var expected = ReflexiveDuelChoice(state, item);
        return choice.ChoiceId == expected.ChoiceId && choice.ChoiceWindow == expected.ChoiceWindow && choice.PlayerId == expected.PlayerId
            && choice.SourceObjectId == expected.SourceObjectId && choice.EffectKind == expected.EffectKind && choice.RequiredCount == 1 && choice.MaxCount == 1
            && choice.LegalObjectIds.Count > 0 && choice.LegalObjectIds.SequenceEqual(expected.LegalObjectIds) && choice.ContextObjectIds.SequenceEqual(expected.ContextObjectIds);
    }
    private static StackResolutionResult ResolveReflexiveDuel(MatchState state, StackItemState item)
    {
        var context = item.ReflexiveDuel!; var result = NoopStackResolutionResult(state);
        var first = context.MovedUnit.ObjectId;
        if (!state.CardObjects.TryGetValue(first, out var firstCard) || firstCard.ObjectGeneration != context.MovedUnit.Generation
            || !FieldObjectTypeRules.IsVisibleUnit(firstCard) || !IsObjectOnField(state.PlayerZones, first)
            || item.TargetObjectIds.SingleOrDefault() is not { } second || !ReflexiveDuelTargets(state, item).Contains(second)
            || item.TargetGenerations?.GetValueOrDefault(second, -1) != state.CardObjects[second].ObjectGeneration) return result;
        var firstPower = ResolveCurrentFieldUnitPower(state, firstCard);
        var secondPower = ResolveCurrentFieldUnitPower(state, state.CardObjects[second]);
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value); var zones = NormalizeZonesForSeats(state);
        var events = new List<GameEvent>(); var destroyed = new HashSet<string>(StringComparer.Ordinal);
        var causes = new Dictionary<string, StackItemState>(StringComparer.Ordinal);
        foreach (var (source, target, amount) in new[] { (first, second, firstPower), (second, first, secondPower) })
        {
            if (amount <= 0) continue;
            var damage = ApplyDamageToCardObject(state, cards, EffectiveFieldControllerId(state.PlayerZones, source, state.CardObjects[source])!, target, amount, destroyed);
            if (damage.DamageAmount > 0) causes[target] = item with { SourceObjectId = source,
                ControllerId = EffectiveFieldControllerId(state.PlayerZones, source, state.CardObjects[source]) ?? item.ControllerId,
                CardNo = state.CardObjects[source].CardNo ?? string.Empty, EffectKind = "UNIT_DAMAGE", ReflexiveDuel = null };
            events.Add(new("DAMAGE_APPLIED", "单位按当前战力互相造成伤害", BuildDamagePayload(item.SourceObjectId, target, damage, source)));
        }
        var locations = state.ObjectLocations.ToDictionary(e => e.Key, e => e.Value);
        var cleanup = RunStateBasedCleanupLoop(state, zones, cards, item, state.RunePools, damageTriggeredDestroyTargetObjectIds: destroyed,
            objectLocations: locations, destroyedUnitOwnerIdsAlreadyThisTurn: state.DestroyedUnitOwnerIdsThisTurn, destructionSources: causes);
        events.AddRange(cleanup.Events);
        return result with { PlayerZones = zones, CardObjects = cards, Events = events, RunePools = cleanup.RunePools,
            TriggerQueue = cleanup.TriggerQueue, DestroyedUnitOwnerIds = cleanup.DestroyedUnitOwnerIds, ObjectLocations = ReconcileObjectLocations(locations, zones) };
    }
}
