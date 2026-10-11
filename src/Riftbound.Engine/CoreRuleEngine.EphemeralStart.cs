using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record EphemeralStartContext(string? CardNo, long SourceGeneration);

public sealed partial class CoreRuleEngine
{
    internal const string EphemeralStartEffect = "CAPTURED_EPHEMERAL_START_DESTROY";

    private static MatchState QueueEphemeralStartAbilities(MatchState state, List<GameEvent> events)
    {
        var queue = state.TriggerQueue.ToList();
        foreach (var source in state.CardObjects.Values.OrderBy(c => c.ObjectId, StringComparer.Ordinal))
        {
            if (!IsObjectOnField(state.PlayerZones, source.ObjectId) || source.IsFaceDown
                || source.Tags.Contains(CardObjectTags.Standby) || !source.Tags.Contains(CardObjectTags.Ephemeral)
                || !IsFieldObjectControlledByZonePlayer(state.PlayerZones, state.CardObjects, source.ObjectId)
                || EffectiveFieldControllerId(state.PlayerZones, source.ObjectId, source) != state.TurnPlayerId
                || !string.IsNullOrWhiteSpace(source.AttachedToObjectId) && source.GrantedTags?.Contains(CardObjectTags.Ephemeral) != true
                || IsEphemeralTurnStartSuppressedByLeblancStatic(state.PlayerZones, state.CardObjects,
                    state.ObjectLocations, state.TurnPlayerId, source.ObjectId)) continue;
            var trigger = new TriggerQueueItemState($"ephemeral-{state.TurnNumber}-{state.Tick}-{source.ObjectId}",
                state.TurnPlayerId, source.ObjectId, EphemeralStartEffect, "TURN_START_BEGAN")
                { EphemeralStart = new(source.CardNo, source.ObjectGeneration), SourceCardNo = source.CardNo };
            queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
        }
        return state with { TriggerQueue = queue };
    }

    internal static bool ValidEphemeralStart(MatchState state, EphemeralStartContext? context, string effect,
        string controller, string source, string? cardNo = null)
        => context is null ? effect != EphemeralStartEffect
            : effect == EphemeralStartEffect && context.SourceGeneration >= 0 && !string.IsNullOrWhiteSpace(source)
                && (cardNo is null || context.CardNo == cardNo) && controller == state.TurnPlayerId
                && state.Phase == MatchPhases.TurnStart && state.TurnStartStep == "SCORE";

    private static StackResolutionResult ResolveEphemeralStart(MatchState state, StackItemState item)
    {
        // A captured skill still references the same field object after movement,
        // control or keyword changes. A new incarnation is a different object.
        if (!state.CardObjects.TryGetValue(item.SourceObjectId, out var source)
            || source.ObjectGeneration != item.EphemeralStart!.SourceGeneration
            || !IsObjectOnField(state.PlayerZones, source.ObjectId)) return NoopStackResolutionResult(state);
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        var removal = ResolveFieldDestructions(state, zones, cards, item, new HashSet<string>(),
            state.DestroyedUnitOwnerIdsThisTurn.ToHashSet(StringComparer.Ordinal), state.RunePools,
            objectLocations: state.ObjectLocations, explicitDestroyObjectIds: new HashSet<string> { source.ObjectId },
            explicitDestroyReason: "EPHEMERAL_TURN_START");
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), RunePools = removal.RunePools,
            TriggerQueue = removal.TriggerQueue, DestroyedUnitOwnerIds = removal.DestroyedUnitOwnerIds, Events = removal.Events };
    }
}
