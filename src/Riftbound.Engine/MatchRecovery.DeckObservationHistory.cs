namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateObservedDeckPlayHistory(MatchState state, IReadOnlyList<RecoveredCommand> commands,
        IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        foreach (var context in state.ObservedDeckPlays ?? [])
        {
            if (context?.Instruction is null || context.Card is null) continue;
            var expected = CoreRuleEngine.ObservedDeckCaptureHash(context);
            var matches = events.Where(e => e.Event.Kind == "CARD_BANISHED"
                && TryReadObjectString(e.Event.Payload, "observationHash", out var hash) && hash == expected
                && TryReadObjectString(e.Event.Payload, "targetObjectId", out var id) && id == context.Card.ObjectId).ToArray();
            if (matches.Length != 1 || !commands.Any(c => c.Accepted && c.CompletedTick == matches[0].Tick
                && c.StartedEventSequence < matches[0].Sequence && matches[0].Sequence <= c.CompletedEventSequence))
                errors.Add("observed deck play differs from its original accepted observation");
        }
    }
}
