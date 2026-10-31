namespace Riftbound.Engine;

/// <summary>CN 190.3 and 464.2.c: the player whose units caused the contest attacks.</summary>
internal static class BattlefieldContestRules
{
    internal const string InitiatorPrefix = "BATTLEFIELD_CONTEST_INITIATOR:";

    /// <summary>
    /// Test/recovery fixtures can import a board that is already contested at
    /// tick zero.  Such a position has no contest-causing command to resume;
    /// creating the normal cleanup/duel tasks would incorrectly block the
    /// first command against the imported state.  A contest created by the
    /// engine records its initiator, so it remains eligible for task creation.
    /// </summary>
    internal static bool IsInitialStaticContest(
        MatchState state,
        BattlefieldState battlefield,
        bool hasStateBasedCleanup)
    {
        if (state.Tick != 0
            || !string.Equals(state.Status, MatchStatuses.InProgress, StringComparison.Ordinal)
            || !string.Equals(state.Phase, MatchPhases.Main, StringComparison.Ordinal)
            || !string.Equals(state.TimingState, TimingStates.NeutralOpen, StringComparison.Ordinal)
            || state.StackItems.Count > 0
            || !string.IsNullOrWhiteSpace(state.PriorityPlayerId)
            || !string.IsNullOrWhiteSpace(state.FocusPlayerId)
            || state.TriggerQueue.Count > 0
            || state.PendingPayment is not null
            || state.PendingHandChoice is not null
            || state.PendingCardChoice is not null
            || state.PendingEffectPlay is not null
            || state.BattleState.IsActive
            || hasStateBasedCleanup)
        {
            return false;
        }

        // A static imported contest is only safe to bypass when both sides are
        // actually present.  A lone occupant whose controller disagrees with
        // the printed battlefield controller is a dirty fixture and must enter
        // the normal control task instead of silently skipping it.
        if (battlefield.OccupantControllerIds.Count < 2)
        {
            return false;
        }

        var prefix = MarkerPrefix(battlefield.BattlefieldObjectId);
        return !state.UntilEndOfTurnEffects.Any(effect => effect.StartsWith(prefix, StringComparison.Ordinal));
    }

    public static string Initiator(MatchState state, BattlefieldState battlefield)
    {
        if (CoreRuleEngine.IsInitialBattleDuel(state, out var captured)
            && captured.BattlefieldId == battlefield.BattlefieldObjectId)
            return captured.InitialAttackingPlayerId!;
        var prefix = MarkerPrefix(battlefield.BattlefieldObjectId);
        var recorded = state.UntilEndOfTurnEffects.FirstOrDefault(effect => effect.StartsWith(prefix, StringComparison.Ordinal));
        if (recorded is not null) return recorded[prefix.Length..];

        // Imported positions may predate contest provenance. A controller's opponent
        // is the contester; ownership of the physical battlefield is irrelevant.
        var opposing = battlefield.OccupantControllerIds
            .Where(player => player != battlefield.ControllerId).ToArray();
        if (opposing.Length == 1) return opposing[0];
        return battlefield.OccupantControllerIds.Contains(state.TurnPlayerId, StringComparer.Ordinal)
            ? state.TurnPlayerId : battlefield.OccupantControllerIds.FirstOrDefault() ?? string.Empty;
    }

    public static MatchState RecordNewContests(MatchState state, MatchState? previous, string causingPlayerId)
    {
        if (previous is null) return state;
        var markers = state.UntilEndOfTurnEffects.ToHashSet(StringComparer.Ordinal);
        var changed = false;
        foreach (var battlefield in state.BattlefieldStates.Values.Where(field => field.Contested))
        {
            previous.BattlefieldStates.TryGetValue(battlefield.BattlefieldObjectId, out var old);
            if (old?.Contested == true) continue;
            var entrants = battlefield.OccupantControllerIds
                .Except(old?.OccupantControllerIds ?? [], StringComparer.Ordinal).ToArray();
            var initiator = entrants.Length == 1 ? entrants[0]
                : battlefield.OccupantControllerIds.Contains(causingPlayerId, StringComparer.Ordinal)
                    ? causingPlayerId : Initiator(state, battlefield);
            if (string.IsNullOrWhiteSpace(initiator)) continue;
            var prefix = MarkerPrefix(battlefield.BattlefieldObjectId);
            markers.RemoveWhere(effect => effect.StartsWith(prefix, StringComparison.Ordinal));
            markers.Add(prefix + initiator);
            changed = true;
        }
        return changed ? state with { UntilEndOfTurnEffects = markers.Order(StringComparer.Ordinal).ToArray() } : state;
    }

    private static string MarkerPrefix(string battlefieldId) => InitiatorPrefix + battlefieldId + ":";
}
