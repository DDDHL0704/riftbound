using System.Text.Json.Serialization;

namespace Riftbound.Engine;

public sealed record RequiredUnitDestructionOrigin(ObjectBinding Battlefield, string? DefendingPlayerId);

public sealed partial record UnitEventContext
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RequiredUnitDestructionOrigin? RequiredUnitDestruction { get; init; }
}

public sealed partial class CoreRuleEngine
{
    private static UnitEventObservation CaptureRequiredUnitDestruction(MatchState state, UnitEventObservation observation)
    {
        if (UnitEventSpec(observation.Context.CardNo)?.RequiredUnitDestructionScope != DefendingPlayerHereDestructionScope)
            return observation;
        if (!TryGetPreciseFieldLocation(state.PlayerZones, state.ObjectLocations, observation.Source, out var location)
            || location.Zone != "BATTLEFIELD" || location.BattlefieldObjectId is not { } id
            || BattlefieldLocalRules.Battlefield(state, id) is not { } field)
            throw new InvalidOperationException("An attack destruction instruction requires its captured battlefield.");
        var defender = BattleLimitedMarkers.ScopeFor(state, id)?.DefendingPlayerId;
        if (defender is null && IsInitialBattleDuel(state, out var duel) && duel.BattlefieldId == id)
            defender = ResolveSingleDefendingPlayerId(state.PlayerZones, duel.DefenderObjectIds);
        if (defender is null && state.BattleState is { } battle && battle.BattlefieldObjectId == id)
            defender = BattleDefendingPlayerId(state, battle);
        // A tick-zero imported contest assigns role flags before its first
        // declaration creates a duel marker or an active combat frame.
        defender ??= ResolveSingleDefendingPlayerId(state.PlayerZones, state.CardObjects.Values
            .Where(card => card.IsDefending && IsObjectLocatedAtBattlefield(state.PlayerZones,
                state.ObjectLocations, card.ObjectId, id)).Select(card => card.ObjectId).ToArray());
        return observation with { Context = observation.Context with {
            RequiredUnitDestruction = new(new(id, field.ObjectGeneration), defender) } };
    }

    private static bool ValidRequiredUnitDestruction(MatchState state, UnitEventContext context,
        UnitEventAbilitySpec spec, string? controller)
        => spec.RequiredUnitDestructionScope is null or OpponentsDestructionScope
            ? context.RequiredUnitDestruction is null
            : spec.RequiredUnitDestructionScope == DefendingPlayerHereDestructionScope && spec.Timing == "ATTACK" && spec.Effect == EachPlayerDestroyUnitEffect
                && context.RequiredUnitDestruction is { Battlefield.Generation: >= 0 } origin
                && BattlefieldLocalRules.Battlefield(state, origin.Battlefield.ObjectId) is { } field
                && field.ObjectGeneration >= origin.Battlefield.Generation
                && (origin.DefendingPlayerId is null || state.Seats.ContainsKey(origin.DefendingPlayerId)
                    && origin.DefendingPlayerId != controller);
}
