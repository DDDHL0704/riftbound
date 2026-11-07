using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static IEnumerable<(string Kind, int Amount, int Floor)> ConfirmedCardCountCostReductions(
        MatchState state, string player, CardBehaviorDefinition behavior)
    {
        if (!CardStaticAbilitySpecRules.TryGetStaticAbility(behavior.CardNo,
            spec => spec.Kind == StaticAbilityKinds.SourceCostReductionPerCardPlayedThisTurn
                && spec.Amount > 0 && spec.MinimumManaCost is >= 0, out var reduction)) yield break;
        // CN 420.4.b explicitly uses Siege Ram to distinguish confirmed cards
        // (including countered spells) from successful card resolution.
        var cards = state.CardPlays.TurnNumber == state.TurnNumber
            ? state.CardPlays.Finalized.Count(play => play.PlayerId == player) : 0;
        yield return ("card", cards * reduction.Amount, reduction.MinimumManaCost!.Value);
    }
}
