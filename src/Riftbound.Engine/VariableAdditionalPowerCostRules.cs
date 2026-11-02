using System.Globalization;

namespace Riftbound.Engine;

// A play-time quantity is a declared additional cost, distinct from an
// instruction payment chosen while the effect resolves (CN 204.3.a).
internal static class VariableAdditionalPowerCostRules
{
    internal const string Prefix = "ADDITIONAL_POWER:";

    internal static IReadOnlyDictionary<string, object?> Metadata { get; } =
        new Dictionary<string, object?> {
            ["choicePrefix"] = Prefix, ["minimum"] = 0, ["maximum"] = int.MaxValue,
            ["defaultAmount"] = 0, ["step"] = 1
        }.AsReadOnly();

    internal static bool TryExtract(IReadOnlyList<string> costs, bool allowed,
        out IReadOnlyList<string> remaining, out int amount, out bool declared)
    {
        remaining = costs;
        amount = 0;
        declared = false;
        var chosen = costs.Where(c => c?.Trim().StartsWith(Prefix, StringComparison.Ordinal) == true).ToArray();
        if (chosen.Length == 0) return true;
        if (!allowed || chosen.Length != 1) return false;
        var choice = chosen[0].Trim();
        var raw = choice[Prefix.Length..];
        if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            || raw != parsed.ToString(CultureInfo.InvariantCulture)) return false;
        amount = parsed;
        declared = true;
        remaining = costs.Where(c => c != chosen[0]).ToArray();
        return true;
    }
}
