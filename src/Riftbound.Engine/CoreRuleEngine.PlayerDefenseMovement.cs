using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial record UnitEventContext
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ObjectBinding? DefendedBattlefield { get; init; }
}

public sealed partial class CoreRuleEngine
{
    private const string PlayerDefenseMoveEffect = "MOVE_SELF_TO_DEFENDED_BATTLEFIELD";

    // CN383.4.f / 466.2.c: the player acquires this role once per battle.
    // A later defender entering the same battle does not grant it again.
    private static TriggerQueueItemState[] CapturePlayerDefenseUnitEvents(MatchState state,
        string battlefield, string eventKind)
    {
        if (BattleLimitedMarkers.ScopeFor(state, battlefield) is not { } scope
            || BattlefieldLocalRules.Battlefield(state, battlefield) is not { } field) return [];
        var sources = state.CardObjects.Values.Where(card =>
            EffectiveFieldControllerId(state.PlayerZones, card.ObjectId, card) == scope.DefendingPlayerId)
            .Select(card => card.ObjectId);
        return CaptureUnitEvents(state.PlayerZones, state.CardObjects, sources, UnitEventTimings.PlayerDefend)
            .Select(observation => observation with { Context = observation.Context with {
                DefendedBattlefield = new(field.ObjectId, field.ObjectGeneration) } })
            .Select((observation, index) => UnitEventTrigger(observation, state.Tick, index,
                eventKind, TimingStates.SpellDuelOpen)).ToArray();
    }

    private static bool ValidPlayerDefenseContext(MatchState state, UnitEventContext context, UnitEventAbilitySpec spec)
        => spec.Timing != UnitEventTimings.PlayerDefend ? context.DefendedBattlefield is null
            : spec.Effect == PlayerDefenseMoveEffect && context.DefendedBattlefield is { Generation: >= 0 } field
                && BattlefieldLocalRules.Battlefield(state, field.ObjectId) is { } current
                && current.ObjectGeneration >= field.Generation;

    private static StackResolutionResult ResolvePlayerDefenseMovement(MatchState state, StackItemState item)
    {
        var context = item.UnitEvent!;
        var field = context.DefendedBattlefield!;
        if (BattlefieldLocalRules.Battlefield(state, field.ObjectId)?.ObjectGeneration != field.Generation
            || !state.CardObjects.TryGetValue(item.SourceObjectId, out var source)
            || source.ObjectGeneration != context.SourceGeneration || !IsFaceUpNonStandbyUnit(source)
            || !IsObjectOnField(state.PlayerZones, source.ObjectId)) return NoopStackResolutionResult(state);
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary();
        var locations = state.ObjectLocations.ToDictionary(); var pools = state.RunePools.ToDictionary();
        var events = new List<GameEvent>();
        // This is effect movement, not the unit's standard-movement ability.
        // Shared execution enforces prohibitions, imposed costs, attachments
        // and movement observers without charging exhaustion or requiring Roam.
        MoveFieldUnitsToChosenLocation(state, zones, cards, locations, pools, item,
            [source.ObjectId], "BATTLEFIELD:" + field.ObjectId, events);
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(locations, zones), RunePools = pools, Events = events };
    }
}
