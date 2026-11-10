using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // CN830.1.b: Deploy permits any battlefield and forbids a Base entry.
    internal static bool IsDeployedEquipment(CardBehaviorDefinition behavior)
        => behavior.PlaysSourceToBaseAsEquipment
            && ParseDelimitedValues(behavior.SourceEquipmentTags).Contains(CardEquipmentKeywordNames.Deploy);

    private static bool DeployedEquipmentDestinationAllowed(MatchState state, CardBehaviorDefinition behavior, string destination)
        => IsDeployedEquipment(behavior) && destination.StartsWith("BATTLEFIELD:", StringComparison.Ordinal)
            && BattlefieldLocalRules.Battlefield(state, destination[12..]) is not null;

    private static bool EffectPlayAllowsDeployment(MatchState state, string player)
        => state.PendingEffectPlay is { DestinationPolicy: "ANY" or "SOURCE_HERE_IF_UNIT", RevealedHand: null } pending
            && pending.PlayerId == player && pending.Parent.PlayedFromStandby is null;

    private static UnitEventObservation[] CaptureFriendlyAttackHere(MatchState state, IEnumerable<string> attackers)
    {
        var batch = attackers.Distinct(StringComparer.Ordinal).Where(state.CardObjects.ContainsKey).ToArray();
        return CaptureUnitEvents(state.PlayerZones, state.CardObjects, state.CardObjects.Keys, "FRIENDLY_ATTACK_HERE")
            .Select(o => (Observer: o, Participants: batch.Where(id =>
                    EffectiveFieldControllerId(state.PlayerZones, id, state.CardObjects[id]) == o.Controller
                    && state.ObjectLocations.TryGetValue(id, out var at) && at.Zone == "BATTLEFIELD"
                    && state.ObjectLocations.TryGetValue(o.Source, out var here) && here.Zone == "BATTLEFIELD"
                    && here.BattlefieldObjectId == at.BattlefieldObjectId)
                .Select(id => new UnitEventOtherObject(new(id, state.CardObjects[id].ObjectGeneration), state.CardObjects[id].CardNo!,
                    state.CardObjects[id].CopyFaceTransitions?.Count ?? 0, state.CardObjects[id].TokenFactoryCardNo)).ToArray()))
            .Where(x => x.Participants.Length > 0)
            .Select(x => x.Observer with { Context = x.Observer.Context with { Attackers = x.Participants } }).ToArray();
    }

    private static string[] UnitEventEnemiesHere(MatchState state, StackItemState item)
    {
        // CN359.3.f.2: "here" is read at resolution from the current source
        // generation. A departed source cannot supply a location.
        var here = CombatMightSourceLocation(state, item.SourceObjectId, item.UnitEvent!.SourceGeneration);
        return here is null ? [] : state.CardObjects.Values
            .Where(card => IsFaceUpNonStandbyUnit(card) && IsObjectOnField(state.PlayerZones, card.ObjectId)
                && EffectiveFieldControllerId(state.PlayerZones, card.ObjectId, card) != item.ControllerId
                && state.ObjectLocations.TryGetValue(card.ObjectId, out var at) && at.Zone == here.Zone
                && (here.Zone == "BASE" ? at.PlayerId == here.PlayerId : at.BattlefieldObjectId == here.BattlefieldObjectId))
            .Select(card => card.ObjectId).Order(StringComparer.Ordinal).ToArray();
    }

    private static StackResolutionResult ResolveUnitEventDamagedEnemyDestruction(MatchState state, StackItemState item)
    {
        // This is an untargeted instruction. Marked damage is read now, so
        // reactions can heal, hurt, or move units before the shared destruction.
        var damaged = UnitEventEnemiesHere(state, item).Where(id => state.CardObjects[id].Damage > 0)
            .ToHashSet(StringComparer.Ordinal);
        if (damaged.Count == 0) return NoopStackResolutionResult(state);
        var cards = state.CardObjects.ToDictionary();
        var zones = NormalizeZonesForSeats(state);
        var locations = state.ObjectLocations.ToDictionary();
        var removal = ResolveFieldDestructions(state, zones, cards, item, new HashSet<string>(),
            state.DestroyedUnitOwnerIdsThisTurn.ToHashSet(), state.RunePools,
            objectLocations: locations, explicitDestroyObjectIds: damaged);
        return NoopStackResolutionResult(state) with { CardObjects = cards, PlayerZones = zones,
            RunePools = removal.RunePools, Events = removal.Events, TriggerQueue = removal.TriggerQueue,
            DestroyedUnitOwnerIds = removal.DestroyedUnitOwnerIds, ObjectLocations = ReconcileObjectLocations(locations, zones) };
    }

    private static StackResolutionResult ResolveUnitEventAreaDamage(MatchState state, StackItemState item, int amount,
        string? primary = null, int? primaryAmount = null, bool onlyPrimary = false)
    {
        // Point damage has already validated its confirmed target. It does
        // not acquire a location restriction from the area-damage consumer.
        var targets = onlyPrimary ? primary is null ? [] : new[] { primary } : UnitEventEnemiesHere(state, item);
        var cards = state.CardObjects.ToDictionary(); var zones = NormalizeZonesForSeats(state);
        var locations = state.ObjectLocations.ToDictionary(); var destroyed = new HashSet<string>(StringComparer.Ordinal);
        var events = new List<GameEvent>();
        foreach (var target in targets)
        {
            if (onlyPrimary && target != primary) continue;
            var damage = ApplyDamageToCardObject(state, cards, item.ControllerId, target,
                ApplySpellSkillDamageBonuses(state, item.ControllerId, target, target == primary ? primaryAmount!.Value : amount, item), destroyed,
                state.UntilEndOfTurnEffects.Contains(PreventSpellAndSkillDamageThisTurnEffectId), PreventSpellAndSkillDamageThisTurnEffectId);
            events.Add(new("DAMAGE_APPLIED", "技能对敌方单位造成伤害", BuildDamagePayload(item.SourceObjectId, target, damage)));
        }
        var cleanup = RunStateBasedCleanupLoop(state, zones, cards, item, state.RunePools,
            damageTriggeredDestroyTargetObjectIds: destroyed, objectLocations: locations,
            destroyedUnitOwnerIdsAlreadyThisTurn: state.DestroyedUnitOwnerIdsThisTurn);
        events.AddRange(cleanup.Events);
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, Events = events,
            RunePools = cleanup.RunePools, TriggerQueue = cleanup.TriggerQueue, DestroyedUnitOwnerIds = cleanup.DestroyedUnitOwnerIds,
            ObjectLocations = ReconcileObjectLocations(locations, zones) };
    }
}
