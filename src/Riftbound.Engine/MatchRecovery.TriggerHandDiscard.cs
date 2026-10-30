namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static readonly string[] PrivateHandDiscardContextNames =
        ["paidUnitTrigger", "discardCost", "triggerHandDiscardCost", "fieldContext"];

    private static void ValidatePrivateHandDiscardCostView(object? payload, List<string> errors)
    {
        // Public stack records carry the declared effect and targets. The
        // authoritative source and unpaid hand-card bindings stay private.
        foreach (var key in PrivateHandDiscardContextNames)
            if (TryReadObjectValue(payload, key, out _))
                errors.Add("public trigger exposes a private hand discard cost or source binding");
    }
}
