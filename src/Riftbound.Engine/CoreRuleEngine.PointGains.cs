using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // Point receipt is distinct from the scoring action (CN 383.4.c/d and
    // SFD official FAQ, Tianna): hold/conquest and replacement draws still occur.
    private static string[] PointGainPreventingSources(MatchState state, string player)
        => PublicStaticAuraSourceObjectIds(state.PlayerZones).Where(id =>
            state.CardObjects.TryGetValue(id, out var source) && !source.IsFaceDown
            && IsObjectOnField(state.PlayerZones, id)
            && EffectiveFieldControllerId(state.PlayerZones, id, source) != player
            && state.ObjectLocations.TryGetValue(id, out var at) && at.Zone == "BATTLEFIELD"
            && at.BattlefieldObjectId is not null
            && StaticLimitsFor(state, source).Any(spec => spec.PreventsOpponentsGainingPointsWhileAtBattlefield))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

    private static GameEvent PointGainPreventedEvent(MatchState state, string player, int amount,
        string reason, string? source, IReadOnlyList<string> preventingSources)
        => new("SCORE_GAIN_PREVENTED", "持续规则阻止玩家获得分数", new Dictionary<string, object?> {
            ["playerId"] = player, ["amount"] = amount, ["reason"] = reason,
            ["sourceObjectId"] = source is null ? null
                : ResolutionResult.VisibleObjectIdForViewer(state, source, ProjectionViewers.Spectator),
            ["preventingSourceObjectIds"] = preventingSources.ToArray() });

    private static bool TryPreventPointGain(MatchState state, string player, int amount,
        string reason, string? source, List<GameEvent> events,
        IReadOnlyDictionary<string, PlayerZones>? zones = null,
        IReadOnlyDictionary<string, CardObjectState>? cards = null,
        IReadOnlyDictionary<string, ObjectLocationState>? locations = null)
    {
        if (amount <= 0) return false;
        var current = state with { PlayerZones = zones ?? state.PlayerZones, CardObjects = cards ?? state.CardObjects,
            ObjectLocations = ReconcileObjectLocations(locations ?? state.ObjectLocations, zones ?? state.PlayerZones) };
        var preventing = PointGainPreventingSources(current, player);
        if (preventing.Length == 0) return false;
        events.Add(PointGainPreventedEvent(current, player, amount, reason, source, preventing));
        return true;
    }
}
