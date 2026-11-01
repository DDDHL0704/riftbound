namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateBattlefieldPermanentPlayUses(MatchState state,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        if (state.UntilEndOfTurnEffects is null) return;
        var start = events.Where(e => e.Event.Kind == "TURN_PLAYER_ADVANCED"
            && TryReadObjectInt(e.Event.Payload, "turnNumber", out var turn) && turn == state.TurnNumber)
            .Select(e => e.Sequence).DefaultIfEmpty(0).Max();
        var accepted = commands.Where(c => c.Accepted).ToLookup(c => c.CompletedTick);
        var expected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in events)
            if (e.Sequence > start && CoreRuleEngine.TryBattlefieldPermanentPlay(e.Event, out var player, out var field)
                && accepted[e.Tick].Any(c => e.Sequence > c.StartedEventSequence && e.Sequence <= c.CompletedEventSequence))
                expected.Add(CoreRuleEngine.BattlefieldPermanentPlayUse(player, field));
        var actual = state.UntilEndOfTurnEffects.Where(use => use.StartsWith(
            CoreRuleEngine.BattlefieldPermanentPlayUsePrefix, StringComparison.Ordinal)).ToArray();
        if (actual.Distinct(StringComparer.Ordinal).Count() != actual.Length || !expected.SetEquals(actual))
            errors.Add("battlefield permanent play uses differ from accepted play history");
    }
}
