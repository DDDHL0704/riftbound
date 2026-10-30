namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidatePrivateUnitHeldStackFields(IReadOnlyList<object?> stack, MatchState state, string viewer, List<string> errors)
    {
        foreach (var row in stack)
        {
            if (TryReadObjectValue(row, "heldContext", out _) || TryReadObjectValue(row, "unitSource", out _))
                errors.Add("private captured Hold context is not part of a public stack view");
            if (!TryReadObjectString(row, "stackItemId", out var id)) continue;
            var captured = state.StackItems.FirstOrDefault(item => item.StackItemId == id && CoreRuleEngine.IsCapturedUnitHeld(state, item));
            if (captured is not null && (!TryReadObjectString(row, "sourceObjectId", out var source)
                || source != ResolutionResult.VisibleObjectIdForViewer(state, captured.SourceObjectId, viewer)))
                errors.Add("captured Hold source visibility differs from authoritative state");
        }
    }
}
