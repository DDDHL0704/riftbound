namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateUnitEventPlayPermissions(MatchState state,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        var boundary = CurrentGameAcceptedEventBoundary(state, commands, events);
        foreach (var frame in new MatchState?[] { state, state.PendingRuleChoice?.Origin })
        {
            if (frame?.PendingEffectPlay is not { } pending || !CoreRuleEngine.IsUnitEventGraveyardPlay(pending.Parent)) continue;
            var original = events.Where(entry => entry.Sequence > boundary
                && entry.Event.Kind == CoreRuleEngine.UnitEventPlayPermissionCaptured
                && TryReadObjectString(entry.Event.Payload, "choiceId", out var id) && id == pending.ChoiceId).ToArray();
            if (original.Length != 1 || !TryReadObjectString(original[0].Event.Payload, "permissionHash", out var hash)
                || hash != CoreRuleEngine.UnitEventPlayPermissionHash(pending)
                || !commands.Any(command => command.Accepted && command.CompletedTick == original[0].Tick
                    && command.StartedEventSequence < original[0].Sequence && command.CompletedEventSequence >= original[0].Sequence))
                errors.Add("unit event play permission differs from the original accepted capture");
        }
    }
}
