using System.Globalization;

namespace Riftbound.Engine;

// An explicit wallet allocation is part of the submitted intent, not a change
// to the printed price. The server validates it again against the final pool.
internal static class ManaSpendSelectionRules
{
    internal const string Prefix = "MANA_SOURCE:";
    internal static readonly string[] SourceKeys = ["DUEL_ONLY", "SPELL_ONLY", "UNIT_PLAY_OR_UNIT_ABILITY_ONLY", "UNRESTRICTED"];

    internal static IReadOnlyDictionary<string, object?> Metadata { get; } = new Dictionary<string, object?> {
        ["choicePrefix"] = Prefix, ["sourceKeys"] = SourceKeys.ToArray(),
        ["componentFormat"] = "<key>:<positiveInt32>", ["separator"] = ",",
        ["maximumComponents"] = 4, ["maximumTotal"] = int.MaxValue,
        ["ordinalKeys"] = true, ["positiveManaCostOnly"] = true,
        ["exactTotal"] = true, ["usePreviewAvailableWallet"] = true };

    internal static bool TryExtract(IReadOnlyList<string> costs, out IReadOnlyList<string> remaining,
        out IReadOnlyDictionary<string, int>? selection)
    {
        selection = null;
        var choices = costs.Where(IsSelection).ToArray();
        remaining = costs.Where(cost => !IsSelection(cost)).ToArray();
        if (choices.Length == 0) return true;
        if (choices.Length != 1 || choices[0] != choices[0].Trim() || choices[0].Length > Prefix.Length + 4 * 50) return false;
        var parts = choices[0][Prefix.Length..].Split(',');
        if (parts.Length is < 1 or > 4) return false;
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        string? previous = null;
        long total = 0;
        foreach (var part in parts)
        {
            var pair = part.Split(':');
            if (pair.Length != 2 || !SourceKeys.Contains(pair[0], StringComparer.Ordinal)
                || previous is not null && string.CompareOrdinal(previous, pair[0]) >= 0
                || pair[1].Length == 0 || pair[1][0] == '0' || pair[1].Any(c => c is < '0' or > '9')
                || !int.TryParse(pair[1], NumberStyles.None, CultureInfo.InvariantCulture, out var amount) || amount <= 0) return false;
            total += amount;
            if (total > int.MaxValue) return false;
            result.Add(pair[0], amount);
            previous = pair[0];
        }
        selection = result;
        return true;

        static bool IsSelection(string cost)
            => !string.IsNullOrWhiteSpace(cost) && cost.Trim().StartsWith(Prefix, StringComparison.Ordinal);
    }
}
