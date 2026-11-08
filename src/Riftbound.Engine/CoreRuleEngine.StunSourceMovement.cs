using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal static string StunSourceMovementCaptureHash(FieldTriggerContext context)
        => MatchStateHasher.HashValue(context with { SourceCeasedToExist = false,
            StunObserver = context.StunObserver! with { CeasedSource = null } });

    internal static bool ValidStunSourceMovementStack(StackItemState item)
        => item.FieldContext?.Kind != StunMoveSourceKind
            || item.TargetObjectIds.Count == 0 && item.TargetGenerations is null
                && item.OptionalCosts.Count == 0 && item.DamageAmount == 0 && item.EffectRepeatCount == 1
                && item.Destination.Length == 0 && !item.SourceConfirmed && item.RepeatExecutions is null
                && item.TargetedPowerAbility is null && item.DamageDistribution is null;

    private static StackResolutionResult ResolveStunSourceMovement(MatchState state, StackItemState item)
    {
        if (!ValidStunSourceMovementStack(item) || item.TriggerCost is null || !ValidTriggerCostReceipt(state, item)
            || item.FieldContext?.StunObserver is not { Battlefield: { } field } birth)
            throw new InvalidOperationException("Invalid confirmed stun source movement.");
        var result = NoopStackResolutionResult(state);
        var events = new List<GameEvent> { new("TRIGGER_RESOLVED", "眩晕后的来源移动技能结算",
            new Dictionary<string, object?> {
                ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, ProjectionViewers.Spectator),
                ["controllerId"] = item.ControllerId, ["effectKind"] = StunMoveSourceKind }) };
        // The stunned unit can have moved or left. Its captured battlefield is
        // retained; a new incarnation of the source cannot perform this move.
        if (!state.CardObjects.TryGetValue(item.SourceObjectId, out var source)
            || source.ObjectGeneration != birth.Source.Generation || !IsFaceUpNonStandbyUnit(source)
            || !IsObjectOnField(state.PlayerZones, source.ObjectId)
            || !state.CardObjects.TryGetValue(field.ObjectId, out var battlefield)
            || battlefield.ObjectGeneration != field.Generation
            || BattlefieldLocalRules.Battlefield(state, field.ObjectId) is null)
            return result with { Events = events };
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var locations = state.ObjectLocations.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var pools = state.RunePools.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        TryMoveFieldUnitByEffect(state, zones, cards, locations, pools, source.ObjectId,
            new(item.ControllerId, "BATTLEFIELD", field.ObjectId), item.ControllerId, item, events);
        var cleanup = RunStateBasedCleanupLoop(state, zones, cards, item, pools,
            objectLocations: locations, destroyedUnitOwnerIdsAlreadyThisTurn: state.DestroyedUnitOwnerIdsThisTurn);
        events.AddRange(cleanup.Events);
        return result with { PlayerZones = zones, CardObjects = cards, RunePools = cleanup.RunePools,
            TriggerQueue = cleanup.TriggerQueue, DestroyedUnitOwnerIds = cleanup.DestroyedUnitOwnerIds,
            ObjectLocations = ReconcileObjectLocations(locations, zones), Events = events };
    }
}
