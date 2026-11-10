using System.Text.Json.Serialization;

namespace Riftbound.Engine;

public sealed partial record UnitEventAbilitySpec
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool RequiresSourcePlayedFromStandby { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool RequiresSourcePlayedOnOwnTurn { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UnitEventSourcePlayCondition(int TurnNumber, string TurnPlayerId, string PlayingPlayerId,
    StandbyPlacement? FromStandby);

public sealed partial record UnitEventContext
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UnitEventSourcePlayCondition? SourcePlayCondition { get; init; }
}

public sealed partial class CoreRuleEngine
{
    private static bool HasSourcePlayCondition(UnitEventAbilitySpec? spec)
        => spec is not null && (spec.RequiresSourcePlayedFromStandby || spec.RequiresSourcePlayedOnOwnTurn
            || spec.RequiresSourcePlayLegendCost);

    // These conditions belong to the source's actual play instruction. A
    // later move, face change, or play of another permanent is irrelevant.
    private static UnitEventObservation[] CaptureSourcePlayConditions(CardObjectState entered, string player,
        UnitEventObservation[] observations)
    {
        if (!observations.Any(o => HasSourcePlayCondition(UnitEventSpec(o.Context.CardNo))))
            return observations;
        var frame = RuleChoices.Value?.ResolutionState ?? RuleChoices.Value?.Origin;
        return observations.Where(o => !HasSourcePlayCondition(UnitEventSpec(o.Context.CardNo))
                || o.Source == entered.ObjectId && o.Controller == player && frame is not null
                    && (!UnitEventSpec(o.Context.CardNo)!.RequiresSourcePlayedFromStandby
                        || entered.LastStandbyPlay is not null)
                    && (!UnitEventSpec(o.Context.CardNo)!.RequiresSourcePlayedOnOwnTurn
                        || frame.TurnPlayerId == o.Controller)
                    && (!UnitEventSpec(o.Context.CardNo)!.RequiresSourcePlayLegendCost
                        || SourcePaidLegendPlayCost(entered, player) is not null))
            .Select(o => !HasSourcePlayCondition(UnitEventSpec(o.Context.CardNo)) ? o
                : o with { Context = o.Context with { SourcePlayCondition = new(frame!.TurnNumber,
                    frame!.TurnPlayerId, player, entered.LastStandbyPlay),
                    PaidLegendPlayCost = UnitEventSpec(o.Context.CardNo)!.RequiresSourcePlayLegendCost
                        ? SourcePaidLegendPlayCost(entered, player) : null } }).ToArray();
    }

    private static bool ValidSourcePlayCondition(MatchState state, UnitEventContext context,
        UnitEventAbilitySpec spec, string source)
    {
        if (!ValidPaidLegendPlayCondition(state, context, spec)) return false;
        if (!HasSourcePlayCondition(spec)) return context.SourcePlayCondition is null;
        if (context.SourcePlayCondition is not { TurnNumber: > 0, TurnPlayerId.Length: > 0,
                PlayingPlayerId.Length: > 0 } condition
            || condition.TurnNumber > state.TurnNumber || !state.Seats.ContainsKey(condition.TurnPlayerId)
            || !state.Seats.ContainsKey(condition.PlayingPlayerId)
            || spec.Timing != "PLAY"
            || spec.RequiresSourcePlayedOnOwnTurn && condition.PlayingPlayerId != condition.TurnPlayerId
            || spec.RequiresSourcePlayedFromStandby && condition.FromStandby is null) return false;
        // An earned skill can later be copied or change controller. Its
        // original play condition is never rechecked against that controller.
        return condition.FromStandby is not { } origin
            || StandbyRules.ValidUnitEventPlayedOrigin(state, origin, source, context.SourceGeneration,
                condition.TurnNumber, condition.PlayingPlayerId);
    }
}
