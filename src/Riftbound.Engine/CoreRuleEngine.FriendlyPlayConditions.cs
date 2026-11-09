using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial record UnitEventAbilitySpec
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool RequiresSpellDuelAtUnitPlay { get; init; }
}

public sealed partial record UnitEventContext
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? PlayedUnitDuringSpellDuel { get; init; }
}

public sealed partial class CoreRuleEngine
{
    private static UnitEventObservation[] CaptureFriendlyPlayConditions(UnitEventObservation[] observations)
    {
        if (!observations.Any(o => UnitEventSpec(o.Context.CardNo)!.RequiresSpellDuelAtUnitPlay))
            return observations;
        // CN383.2 / 420.4: observe the actual play instruction. A duel started
        // by subsequent cleanup cannot retroactively earn this play skill.
        var frame = RuleChoices.Value!;
        var duringDuel = (frame.ResolutionState ?? frame.Origin).SpellDuelState.IsActive;
        return observations.Where(o => !UnitEventSpec(o.Context.CardNo)!.RequiresSpellDuelAtUnitPlay || duringDuel)
            .Select(o => UnitEventSpec(o.Context.CardNo)!.RequiresSpellDuelAtUnitPlay
                ? o with { Context = o.Context with { PlayedUnitDuringSpellDuel = true } } : o).ToArray();
    }

    private static bool ValidFriendlyPlayCondition(UnitEventContext context, UnitEventAbilitySpec spec)
        => spec.RequiresSpellDuelAtUnitPlay
            ? spec.Timing == "FRIENDLY_UNIT_PLAYED" && context.PlayedUnitDuringSpellDuel == true
            : context.PlayedUnitDuringSpellDuel is null;

    private static StackResolutionResult ResolveUnitEventExperience(MatchState state, StackItemState item,
        UnitEventAbilitySpec spec)
    {
        if (LeadingTriggerCost(item) is not null && item.TriggerCost is null)
            return NoopStackResolutionResult(state);
        var events = new List<GameEvent>();
        var experience = GainExperience(NormalizeExperienceForSeats(state), item.ControllerId, spec.Amount,
            item, events, ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, ProjectionViewers.Spectator),
            item.UnitEvent!.CardNo);
        return NoopStackResolutionResult(state) with { PlayerExperience = experience, Events = events };
    }
}
