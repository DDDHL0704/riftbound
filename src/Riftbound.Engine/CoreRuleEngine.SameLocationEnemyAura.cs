using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // The source's base and each battlefield are distinct locations. Match
    // current control, status, visibility and location rather than card owner.
    internal static string[] SameLocationFilteredEnemyUnitIds(MatchState state, string sourceId,
        string controller, StaticAuraSpec aura)
    {
        if (!state.CardObjects.TryGetValue(sourceId, out var source) || !IsFaceUpNonStandbyUnit(source)
            || !IsObjectOnField(state.PlayerZones, sourceId)) return [];
        return state.CardObjects.Values.Where(candidate => IsFaceUpNonStandbyUnit(candidate)
                && IsObjectOnField(state.PlayerZones, candidate.ObjectId)
                && EffectiveFieldControllerId(state.PlayerZones, candidate.ObjectId, candidate) != controller
                && StaticAuraSpecRules.TargetMatchesFilter(aura, candidate)
                && IsSameFieldLocation(state.PlayerZones, state.ObjectLocations, sourceId, candidate.ObjectId))
            .Select(candidate => candidate.ObjectId).Order(StringComparer.Ordinal).ToArray();
    }
}
