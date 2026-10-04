namespace Riftbound.Engine;

// CN core 429.3: resource-gaining reactions may be used in a response window.
internal static class ResourceActionWindow
{
    internal static bool CanAct(MatchState state, string playerId)
    {
        if (state.Phase != MatchPhases.Main || state.Status != MatchStatuses.InProgress)
            return false;
        if (state.PendingEffectPlay is { } pending) return pending.PlayerId == playerId;
        return state.TimingState switch
        {
            TimingStates.NeutralOpen => state.ActivePlayerId == playerId && state.StackItems.Count == 0,
            TimingStates.SpellDuelOpen => state.FocusPlayerId == playerId,
            TimingStates.NeutralClosed or TimingStates.SpellDuelClosed => state.PriorityPlayerId == playerId,
            _ => false
        };
    }
}
