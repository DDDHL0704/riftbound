namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateUnitEventMovementDeclarations(MatchState state,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        var boundary = CurrentGameAcceptedEventBoundary(state, commands, events);
        var declarations = events.Where(record => record.Sequence > boundary
            && record.Event.Kind == "TRIGGER_MOVEMENT_DECLARED").ToArray();
        foreach (var frame in new MatchState?[] { state, state.PendingRuleChoice?.Origin })
        {
            if (frame is null) continue;
            foreach (var item in frame.StackItems.Where(CoreRuleEngine.RequiresUnitEventMovementDeclarationHistory))
            {
                var identity = MatchStateHasher.HashValue(item.StackItemId);
                var originals = declarations.Where(record => TryReadObjectString(record.Event.Payload,
                    "declaredUnitEventHash", out var hash) && hash == identity).ToArray();
                if (originals.Length != 1
                    || !commands.Any(command => command.Accepted && command.CompletedTick == originals[0].Tick
                        && command.StartedEventSequence < originals[0].Sequence
                        && command.CompletedEventSequence >= originals[0].Sequence)
                    || !TryReadObjectString(originals[0].Event.Payload, "declaredMovementHash", out var original)
                    || original != CoreRuleEngine.DeclaredUnitEventMovementHash(item))
                    errors.Add("unit event movement differs from its original accepted target and destination declaration");
            }
        }
    }
}
