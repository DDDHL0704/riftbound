using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ScoringActionOrigin(string PlayerId, ObjectBinding Battlefield, string Reason);

public sealed partial record UnitEventContext
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ScoringActionOrigin? ScoringAction { get; init; }
}

public sealed partial class CoreRuleEngine
{
    internal const string OpponentScoringActionTiming = "OPPONENT_SCORED";

    // CN470.1–470.3: scoring is a Hold or Conquest action, distinct from
    // gaining points. Its point may be prevented or replaced by a draw.
    private static GameEvent CaptureOpponentScoringAction(GameEvent ev, MatchState state,
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards,
        string player, string battlefield, string reason)
    {
        var fact = new ScoringActionOrigin(player, new(battlefield, cards[battlefield].ObjectGeneration), reason);
        return WithUnitEvents(ev, CaptureUnitEvents(zones, cards,
                cards.Values.Where(c => EffectiveFieldControllerId(zones, c.ObjectId, c) != player)
                    .Select(c => c.ObjectId), OpponentScoringActionTiming)
            .Select(o => o with { Context = o.Context with { ScoringAction = fact } }).ToArray());
    }

    private static bool ValidScoringActionOrigin(MatchState state, UnitEventContext context,
        UnitEventAbilitySpec spec, string? controller)
        => spec.Timing == OpponentScoringActionTiming
            ? context.ScoringAction is { Battlefield: { Generation: >= 0 } field } origin
                && !string.IsNullOrWhiteSpace(field.ObjectId) && state.Seats.ContainsKey(origin.PlayerId)
                && origin.PlayerId != controller
                && origin.Reason is "BATTLEFIELD_CONQUERED_SCORE" or "BATTLEFIELD_HELD_SCORE"
            : context.ScoringAction is null;
}
