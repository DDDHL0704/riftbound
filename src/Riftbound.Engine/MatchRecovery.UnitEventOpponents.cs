namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateUnitEventOpponentHistory(MatchState state,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        if (!state.StackItems.Any(item => item.UnitEvent?.ChosenOpponentId is not null)
            && state.PendingRuleChoice?.Origin.StackItems.Any(item => item.UnitEvent?.ChosenOpponentId is not null) != true
            && !events.Any(entry => entry.Event.Kind == "UNIT_EVENT_OPPONENT_CONFIRMED")) return;
        var boundary = CurrentGameAcceptedEventBoundary(state, commands, events);
        var confirmations = events.Where(entry => entry.Sequence > boundary
            && entry.Event.Kind == "UNIT_EVENT_OPPONENT_CONFIRMED").ToArray();
        foreach (var frame in new[] { state, state.PendingRuleChoice?.Origin }.OfType<MatchState>())
        {
            var pending = new Dictionary<string, (long Tick, string Hash)>(StringComparer.Ordinal);
            foreach (var entry in confirmations.Where(entry => entry.Tick <= frame.Tick))
            {
                if (!TryReadObjectString(entry.Event.Payload, "stackItemId", out var id) || id is null
                    || !TryReadObjectString(entry.Event.Payload, "opponentConfirmationHash", out var hash) || hash is null
                    || commands.Count(command => command.Accepted && command.CompletedTick == entry.Tick
                        && command.StartedEventSequence < entry.Sequence && command.CompletedEventSequence >= entry.Sequence) != 1
                    || !pending.TryAdd(id, (entry.Tick, hash)))
                    errors.Add("unit-event opponent lacks its unique original accepted confirmation");
            }
            foreach (var entry in events.Where(entry => entry.Sequence > boundary && entry.Tick <= frame.Tick
                && entry.Event.Kind is "STACK_ITEM_RESOLVED" or "STACK_ITEM_COUNTERED"))
                if (TryReadObjectString(entry.Event.Payload, "stackItemId", out var id) && id is not null) pending.Remove(id);
            // An earned delayed skill inherits the opponent from its original
            // scheduled-unit-event receipt, rather than choosing a new player.
            var items = frame.StackItems.Where(item => item.UnitEvent is { ChosenOpponentId: not null, DelayedReceipt: null }).ToArray();
            foreach (var item in items)
                if (!pending.TryGetValue(item.StackItemId, out var original)
                    || original.Tick != item.UnitEvent!.OpponentConfirmedAtTick
                    || original.Hash != CoreRuleEngine.UnitEventOpponentConfirmationHash(item))
                    errors.Add("unit-event opponent differs from its original accepted confirmation");
            if (frame.Status == MatchStatuses.InProgress && !pending.Keys.ToHashSet(StringComparer.Ordinal)
                    .SetEquals(items.Select(item => item.StackItemId)))
                errors.Add("pending unit-event opponent selections differ from original accepted history");
        }
    }
}
