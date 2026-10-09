namespace Riftbound.Engine;

// CN 312.2.c-d, 429.3, 813.1.c.2: response rights are not limited to main phase.
internal static class ResourceActionWindow
{
    internal static bool CanAct(MatchState state, string playerId)
    {
        if (state.Status != MatchStatuses.InProgress) return false;
        if (state.PendingCardChoice is { ChoiceWindow: "TRIGGER_CONFIRMATION" } confirmation) return confirmation.PlayerId == playerId;
        if (state.PendingPayment is { } payment) return payment.PlayerId == playerId;
        if (state.PendingEffectPlay is { } pending) return pending.PlayerId == playerId;
        if (state.Phase is not (MatchPhases.Main or MatchPhases.TurnStart or MatchPhases.TurnEnd)) return false;
        return state.TimingState switch
        {
            TimingStates.NeutralOpen => state.Phase == MatchPhases.Main
                && state.ActivePlayerId == playerId && state.StackItems.Count == 0,
            TimingStates.SpellDuelOpen => state.FocusPlayerId == playerId,
            TimingStates.NeutralClosed or TimingStates.SpellDuelClosed => state.PriorityPlayerId == playerId,
            _ => false
        };
    }
}
