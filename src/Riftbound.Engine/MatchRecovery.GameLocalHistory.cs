namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static long CurrentGameAcceptedEventBoundary(MatchState state,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events)
        => events.Where(record => record.Event.Kind == "MATCH_STARTED"
                || state.Status == MatchStatuses.Seating && record.Event.Kind == "SERIES_GAME_COMPLETED")
            .Where(record => commands.Any(command => command.Accepted && command.CompletedTick == record.Tick
                && command.StartedEventSequence < record.Sequence && command.CompletedEventSequence >= record.Sequence))
            .Select(record => record.Sequence).DefaultIfEmpty().Max();
}
