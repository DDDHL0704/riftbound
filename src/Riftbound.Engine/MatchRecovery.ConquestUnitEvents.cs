namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateFirstConquestUseHistory(MatchState state, IReadOnlyList<RecoveredCommand> commands,
        IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        var expected = events.Where(e => e.Event.Kind == "TRIGGER_QUEUED"
                && TryReadObjectLong(e.Event.Payload, "firstConquestTurn", out var turn) && turn == state.TurnNumber
                && commands.Any(c => c.Accepted && c.CompletedTick == e.Tick
                    && c.StartedEventSequence < e.Sequence && c.CompletedEventSequence >= e.Sequence))
            .Select(e => TryReadObjectString(e.Event.Payload, "firstConquestUse", out var use) ? use : null)
            .OfType<string>().ToHashSet(StringComparer.Ordinal);
        // Structural validation reports malformed lists and entries. History
        // validation must still reject drift without throwing on those values.
        var actual = (state.UntilEndOfTurnEffects ?? []).OfType<string>()
            .Where(e => e.StartsWith(CoreRuleEngine.FirstConquestUsePrefix, StringComparison.Ordinal)).ToArray();
        if (actual.Distinct(StringComparer.Ordinal).Count() != actual.Length || !expected.SetEquals(actual))
            errors.Add("first conquest skill uses differ from their original accepted captures");
    }
}
