namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateArmamentLinkedExileHistory(MatchState state,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in events.Where(value => value.Event.Kind == "ARMAMENT_LINKED_EXILE_CREATED")
            .OrderBy(value => value.Sequence))
        {
            if (!TryReadObjectString(entry.Event.Payload, "instanceHash", out var key) || key is null
                || !TryReadObjectString(entry.Event.Payload, "linkHash", out var hash) || hash is null
                || !commands.Any(command => command.Accepted && command.CompletedTick == entry.Tick
                    && command.StartedEventSequence < entry.Sequence && entry.Sequence <= command.CompletedEventSequence))
            {
                errors.Add("armament linked exile lacks its original accepted banishment event");
                continue;
            }
            expected[key] = hash;
        }
        var actual = state.LinkedExiles.Where(group => group.ArmamentOrigin is not null).ToArray();
        if (actual.Length != expected.Count || actual.Any(group =>
            !expected.TryGetValue(CoreRuleEngine.LinkedArmamentExileKey(group), out var hash)
                || hash != MatchStateHasher.HashValue(group)))
            errors.Add("armament linked exile ledger differs from its original accepted banishment history");
    }
}
