namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateUnitEventHandChoiceHistory(MatchState state,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        if (!state.StackItems.Any(CoreRuleEngine.IsUnitEventHandChoice)
            && state.PendingRuleChoice?.Origin.StackItems.Any(CoreRuleEngine.IsUnitEventHandChoice) != true) return;
        var boundary = CurrentGameAcceptedEventBoundary(state, commands, events);
        foreach (var frame in new[] { state, state.PendingRuleChoice?.Origin }.OfType<MatchState>())
        {
            var original = events.Where(entry => entry.Sequence > boundary && entry.Tick <= frame.Tick).ToArray();
            bool Accepted(RecoveredEvent entry) => commands.Count(command => command.Accepted
                && command.CompletedTick == entry.Tick && command.StartedEventSequence < entry.Sequence
                && command.CompletedEventSequence >= entry.Sequence) == 1;
            var births = original.Where(entry => entry.Event.Kind == "HAND_REVEALED"
                && TryReadObjectString(entry.Event.Payload, "unitEventHandChoiceHash", out var hash)
                && !string.IsNullOrWhiteSpace(hash)).ToArray();
            var completed = original.Where(entry => entry.Event.Kind == "CARD_CHOICE_RESOLVED"
                && TryReadObjectString(entry.Event.Payload, "unitEventHandStackId", out var id)
                && !string.IsNullOrWhiteSpace(id)).ToArray();
            var pending = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in births)
                if (!Accepted(entry) || !TryReadObjectString(entry.Event.Payload, "choiceId", out var id)
                    || string.IsNullOrWhiteSpace(id)
                    || !TryReadObjectString(entry.Event.Payload, "unitEventHandChoiceHash", out var hash)
                    || string.IsNullOrWhiteSpace(hash) || !pending.TryAdd(id, hash))
                    errors.Add("unit-event hand reveal lacks its unique original accepted observation");
            foreach (var entry in completed)
                if (!Accepted(entry) || !TryReadObjectString(entry.Event.Payload, "choiceId", out var id)
                    || id is null || !pending.Remove(id))
                    errors.Add("unit-event hand choice completed without its original accepted reveal");
            var items = frame.StackItems.Where(CoreRuleEngine.IsUnitEventHandChoice).ToArray();
            foreach (var item in items)
            {
                var executions = completed.Where(entry => TryReadObjectString(entry.Event.Payload, "unitEventHandStackId", out var id)
                    && id == item.StackItemId).ToArray();
                if (executions.Length != item.CompletedHandExecutions
                    || executions.Select(entry => TryReadObjectInt(entry.Event.Payload, "unitEventHandExecution", out var index)
                        ? index : -1).Order().SequenceEqual(Enumerable.Range(0, item.CompletedHandExecutions)) == false)
                    errors.Add("unit-event hand execution differs from original accepted completion");
            }
            var actual = frame.PendingCardChoice is { HandContext: { } context } choice
                && items.Any(item => item.StackItemId == context.StackItemId) ? choice : null;
            if (actual is not null && (!pending.TryGetValue(actual.ChoiceId, out var originalHash)
                    || originalHash != MatchStateHasher.HashValue(actual.HandContext)))
                errors.Add("unit-event hand choice differs from original accepted reveal");
            if (frame.Status == MatchStatuses.InProgress && !pending.Keys.ToHashSet(StringComparer.Ordinal)
                    .SetEquals(actual is null ? [] : new[] { actual.ChoiceId }))
                errors.Add("pending unit-event hand choices differ from original accepted history");
        }
    }
}
