namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidatePrivateUnitHeldStackFields(IReadOnlyList<object?> stack, List<string> errors)
    {
        foreach (var row in stack)
            if (TryReadObjectValue(row, "heldContext", out _) || TryReadObjectValue(row, "unitSource", out _))
                errors.Add("private captured Hold context is not part of a public stack view");
    }
}
