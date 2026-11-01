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

    private static StackResolutionResult ResolveUnitEventAreaDamage(MatchState state, StackItemState item, int amount)
    {
        // CN359.3.f.2: "here" refers to the source's location when this
        // instruction executes; a departed source has no current location.
        var here = CombatMightSourceLocation(state, item.SourceObjectId, item.UnitEvent!.SourceGeneration);
        if (here is null) return NoopStackResolutionResult(state);
        var targets = state.CardObjects.Values.Where(c => IsFaceUpNonStandbyUnit(c) && IsObjectOnField(state.PlayerZones, c.ObjectId)
            && EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) != item.ControllerId
            && state.ObjectLocations.TryGetValue(c.ObjectId, out var at) && at.Zone == here.Zone
            && (here.Zone == "BASE" ? at.PlayerId == here.PlayerId : at.BattlefieldObjectId == here.BattlefieldObjectId))
            .Select(c => c.ObjectId).Order(StringComparer.Ordinal).ToArray();
        var cards = state.CardObjects.ToDictionary(); var zones = NormalizeZonesForSeats(state);
        var locations = state.ObjectLocations.ToDictionary(); var destroyed = new HashSet<string>(StringComparer.Ordinal);
        var events = new List<GameEvent>();
        foreach (var target in targets)
        {
            var damage = ApplyDamageToCardObject(state, cards, item.ControllerId, target,
                ApplySpellSkillDamageBonuses(state, item.ControllerId, target, amount), destroyed,
                state.UntilEndOfTurnEffects.Contains(PreventSpellAndSkillDamageThisTurnEffectId), PreventSpellAndSkillDamageThisTurnEffectId);
            events.Add(new("DAMAGE_APPLIED", "技能对此处敌方单位造成伤害", BuildDamagePayload(item.SourceObjectId, target, damage)));
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
