using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // CN383.2.a.1: conditions immediately following a trigger are checked at
    // capture. Responses cannot revoke an already earned skill.
    private static bool UnitEventCaptureConditionSatisfied(MatchState state, UnitEventObservation observation)
    {
        if (UnitEventSpec(observation.Context.CardNo)?.RequiresIsolatedEnemyHere != true) return true;
        if (!state.ObjectLocations.TryGetValue(observation.Source, out var here)
            || here.Zone != "BATTLEFIELD" || string.IsNullOrWhiteSpace(here.BattlefieldObjectId)) return false;
        return state.CardObjects.Values.Where(card => IsFaceUpNonStandbyUnit(card)
                && IsObjectOnField(state.PlayerZones, card.ObjectId)
                && EffectiveFieldControllerId(state.PlayerZones, card.ObjectId, card) != observation.Controller
                && state.ObjectLocations.TryGetValue(card.ObjectId, out var location)
                && location.Zone == "BATTLEFIELD" && location.BattlefieldObjectId == here.BattlefieldObjectId)
            .GroupBy(card => EffectiveFieldControllerId(state.PlayerZones, card.ObjectId, card), StringComparer.Ordinal)
            .Any(group => group.Count() == 1);
    }

    private static StackResolutionResult AppendIndependentUnitEventExperience(MatchState state, StackItemState item,
        StackResolutionResult result)
    {
        var spec = SelectedUnitEventSpec(item.UnitEvent!)!;
        if (spec.IndependentExperienceGain <= 0) return result;
        // These source-only instructions have no mid-resolution choice or
        // instruction payment. CN055 keeps the independent experience even
        // when the original source can no longer be readied or modified.
        if (spec.Effect is not ("READY_SELF" or "MIGHT_SELF") || spec.ConsumesSourceBoonDuringResolution)
            throw new InvalidOperationException("Independent experience requires a supported source-only instruction.");
        var events = result.Events.ToList();
        var experience = GainExperience(result.PlayerExperience, item.ControllerId, spec.IndependentExperienceGain,
            item, events, ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, ProjectionViewers.Spectator),
            item.UnitEvent!.CardNo);
        return result with { PlayerExperience = experience, Events = events };
    }
}
