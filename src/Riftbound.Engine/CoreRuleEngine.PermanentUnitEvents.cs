using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // CN359.3.f.2: an instruction reads the source's current location in the
    // captured generation. Leaving and re-entering cannot supply a new "here".
    private static string? UnitEventHereDestination(MatchState state, StackItemState item)
        => CombatMightSourceLocation(state, item.SourceObjectId, item.UnitEvent!.SourceGeneration)
            is { Zone: "BATTLEFIELD", BattlefieldObjectId: { } field } ? "BATTLEFIELD:" + field : null;

    private static bool UnitEventTargetIsHere(MatchState state, StackItemState item, string target)
        => CombatMightSourceLocation(state, item.SourceObjectId, item.UnitEvent!.SourceGeneration) is { } here
            && state.ObjectLocations.TryGetValue(target, out var at) && at.Zone == here.Zone
            && (here.Zone == "BASE" ? at.PlayerId == here.PlayerId : at.BattlefieldObjectId == here.BattlefieldObjectId);

    private static StackResolutionResult ResolvePermanentUnitEventInstruction(MatchState state,
        StackItemState item, UnitEventAbilitySpec spec)
    {
        if ((spec.LeadingManaCost > 0 || spec.LeadingPowerCost > 0) && item.TriggerCost is null)
            return NoopStackResolutionResult(state);
        if (spec.Effect == "CREATE_UNIT_TOKEN")
            return CreateUnitTokenBatch(state, item, spec.CreatedTokenFactoryCardNo!, spec.Amount, false);
        if (item.TargetObjectIds.SingleOrDefault() is not { } target
            || !StackObjectRules.ReferencesCurrentObject(state, item, target)
            || !UnitEventTargets(state, item).Contains(target)) return NoopStackResolutionResult(state);

        var cards = state.CardObjects.ToDictionary();
        var events = new List<GameEvent>();
        var behavior = new CardBehaviorDefinition(item.CardNo, "永久物触发技能", 0, item.EffectKind, 0, 1);
        if (spec.Effect == "BOON_FRIENDLY")
        {
            cards[target] = ApplyBoon(cards[target], behavior, item, target, out var granted, state.PlayerZones, cards);
            events.AddRange(granted);
        }
        else if (spec.Effect == "READY_HERE")
        {
            cards[target] = ApplyReadyState(cards[target], behavior with { ReadiesTarget = true }, item, target,
                out var readied, state.PlayerZones, cards);
            if (readied is not null) events.Add(readied);
        }
        else if (spec.Effect == "MOVE_ENEMY_HERE" && UnitEventHereDestination(state, item) is { } destination)
        {
            var zones = NormalizeZonesForSeats(state);
            var locations = state.ObjectLocations.ToDictionary();
            var pools = state.RunePools.ToDictionary();
            MoveFieldUnitsToChosenLocation(state, zones, cards, locations, pools, item, [target], destination, events);
            return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
                ObjectLocations = ReconcileObjectLocations(locations, zones), RunePools = pools, Events = events };
        }
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = events };
    }
}
