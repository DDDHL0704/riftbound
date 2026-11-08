namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateActivationBanishHistory(MatchState state, IReadOnlyList<RecoveredCommand> commands,
        IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        var boundary = CurrentGameAcceptedEventBoundary(state, commands, events);
        var gameEvents = events.Where(entry => entry.Sequence > boundary).ToArray();
        bool Accepted(RecoveredEvent entry) => commands.Any(command => command.Accepted
            && command.CompletedTick == entry.Tick && command.StartedEventSequence < entry.Sequence
            && command.CompletedEventSequence >= entry.Sequence);
        var captured = gameEvents.Where(entry => entry.Event.Kind == "STACK_ITEM_ADDED"
            && TryReadObjectString(entry.Event.Payload, "activationBanishCostHash", out _) && Accepted(entry))
            .Select(entry => TryReadObjectString(entry.Event.Payload, "stackItemId", out var id) ? id : null)
            .OfType<string>().ToHashSet(StringComparer.Ordinal);
        var retired = gameEvents.Where(entry => entry.Event.Kind is "STACK_ITEM_RESOLVED" or "STACK_ITEM_COUNTERED" && Accepted(entry))
            .Select(entry => TryReadObjectString(entry.Event.Payload, "stackItemId", out var id) ? id : null)
            .OfType<string>().ToHashSet(StringComparer.Ordinal);
        static IEnumerable<StackItemState> Scope(MatchState? frame)
            => frame is null ? [] : frame.StackItems.Concat(frame.PendingEffectPlay is { } pending ? [pending.Parent] : []);
        var items = Scope(state).Concat(Scope(state.PendingRuleChoice?.Origin)).ToArray();
        foreach (var scope in new MatchState?[] { state, state.PendingRuleChoice?.Origin })
            if (Scope(scope).Where(item => item.SimpleActivation?.BanishCost is not null || captured.Contains(item.StackItemId))
                .GroupBy(item => item.StackItemId).Any(group => group.Count() > 1))
                errors.Add("a source banish activation is duplicated across pending locations");
        if (state.Status == MatchStatuses.InProgress
            && !captured.Except(retired).ToHashSet(StringComparer.Ordinal).SetEquals(
                items.Where(item => item.SimpleActivation?.BanishCost is not null || captured.Contains(item.StackItemId))
                    .Select(item => item.StackItemId)))
            errors.Add("pending source banish activations differ from original accepted completion history");
        foreach (var item in items)
        {
            var added = events.Where(entry => entry.Event.Kind == "STACK_ITEM_ADDED"
                && TryReadObjectString(entry.Event.Payload, "stackItemId", out var id) && id == item.StackItemId).ToArray();
            if (item.SimpleActivation?.BanishCost is null
                && !added.Any(entry => TryReadObjectString(entry.Event.Payload, "activationBanishCostHash", out _))) continue;
            if (retired.Contains(item.StackItemId) || item.SimpleActivation is not { } context || added.Length != 1
                || !TryReadObjectString(added[0].Event.Payload, "activationBanishCostHash", out var hash)
                || hash != CoreRuleEngine.ActivationBanishCaptureHash(context, item.ControllerId)
                || !commands.Any(command => command.Accepted && command.PlayerId == item.ControllerId
                    && command.CompletedTick == added[0].Tick && command.StartedEventSequence < added[0].Sequence
                    && command.CompletedEventSequence >= added[0].Sequence))
            { errors.Add("activation banish cost differs from original accepted payment"); continue; }
            var refused = new List<ObjectBinding>();
            foreach (var entry in events.Where(entry => entry.Event.Kind == "EFFECT_PLAY_PAYMENT_REFUSED"
                && TryReadObjectString(entry.Event.Payload, "parentStackItemId", out var id) && id == item.StackItemId))
            {
                if (!TryReadObjectString(entry.Event.Payload, "sourceObjectId", out var source)
                    || string.IsNullOrWhiteSpace(source)
                    || !TryReadObjectLong(entry.Event.Payload, "sourceGeneration", out var generation)
                    || !commands.Any(command => command.Accepted && command.PlayerId == item.ControllerId
                        && command.CompletedTick == entry.Tick && command.StartedEventSequence < entry.Sequence
                        && command.CompletedEventSequence >= entry.Sequence))
                { errors.Add("invalid accepted effect play refusal"); continue; }
                refused.Add(new(source, generation));
            }
            if (!refused.SequenceEqual(context.RefusedEffectPlays ?? []))
                errors.Add("remaining linked plays differ from original accepted refusals");
        }
    }
}
