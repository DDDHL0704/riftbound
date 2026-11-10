using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial record UnitEventAbilitySpec
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? BeginningExactHandCount { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? BeginningExactBattlefieldUnitCount { get; init; }
}

public sealed partial class CoreRuleEngine
{
    internal const string BeginningWinEffect = "WIN_GAME";

    // CN383.2.a.1: the condition belongs to the timing. Later responses
    // cannot erase an earned beginning ability by changing these counts.
    private static bool BeginningUnitEventConditionSatisfied(MatchState state, UnitEventObservation observation)
    {
        var spec = UnitEventSpec(observation.Context.CardNo)!;
        return (spec.BeginningExactHandCount is not { } hand || state.PlayerZones[observation.Controller].Hand.Count == hand)
            && (spec.BeginningExactBattlefieldUnitCount is not { } units
                || state.CardObjects.Values.Count(c => IsFaceUpNonStandbyUnit(c)
                    && EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) == observation.Controller
                    && IsObjectOnField(state.PlayerZones, c.ObjectId)
                    && state.ObjectLocations.TryGetValue(c.ObjectId, out var at) && at.Zone == "BATTLEFIELD"
                    && BattlefieldLocalRules.Battlefield(state, at.BattlefieldObjectId) is not null) == units);
    }

    private static StackResolutionResult ResolveUnitEventWin(MatchState state, StackItemState item)
        => NoopStackResolutionResult(state) with { WinnerPlayerId = item.ControllerId,
            Events = [new("MATCH_WON", "触发技能指示其控制者获胜", new Dictionary<string, object?> {
                ["winnerPlayerId"] = item.ControllerId, ["reason"] = BeginningWinEffect,
                ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, ProjectionViewers.Spectator) })] };
}
