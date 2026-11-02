using System.Text.RegularExpressions;

namespace Riftbound.CardCatalog;

public static class UnitTokenLocationParser
{
    public static bool AllowsOrdinaryLocationChoice(string? text, string? tokenFamily)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(tokenFamily)) return false;
        var compact = Regex.Replace(text, @"\s+", "");
        var instructions = Regex.Split(compact, "[。；]").Where(part =>
            part.Contains("打出", StringComparison.Ordinal) && part.Contains(tokenFamily, StringComparison.Ordinal)).ToArray();
        // An explicit location remains binding when another effect expands the
        // ordinary play locations. This also covers a captured movement origin.
        return instructions.Length == 1 && !Regex.IsMatch(instructions[0], "基地|此处|该处|战场|位置");
    }
}
