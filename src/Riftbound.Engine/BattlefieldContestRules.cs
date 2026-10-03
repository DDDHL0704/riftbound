namespace Riftbound.Engine;

/// <summary>CN 190.3 and 464.2.c: the player whose units caused the contest attacks.</summary>
internal static class BattlefieldContestRules
{
    internal const string InitiatorPrefix = "BATTLEFIELD_CONTEST_INITIATOR:";

    public static string Initiator(MatchState state, BattlefieldState battlefield)
    {
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
