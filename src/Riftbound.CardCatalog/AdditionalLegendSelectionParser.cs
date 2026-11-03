using System.Text.RegularExpressions;

namespace Riftbound.CardCatalog;

public sealed record AdditionalLegendSelectionSpec(string RequiredCardName, int MaximumCount);

public static class AdditionalLegendSelectionParser
{
    public static AdditionalLegendSelectionSpec? Parse(string? text)
    {
        // The current CN erratum says "up to three". The printed requirement
        // to choose exactly three must not supply this optional permission.
        var match = Regex.Match(text ?? "", @"如果你的卡组中包含(?<name>[^，。\r\n]+)，则在起始传奇之外，选择最多(?<count>[0-9]+|[一二三四五六七八九])名不同的传奇。",
            RegexOptions.CultureInvariant);
        if (!match.Success) return null;
        var count = match.Groups["count"].Value;
        var maximum = int.TryParse(count, out var number) ? number : "一二三四五六七八九".IndexOf(count, StringComparison.Ordinal) + 1;
        return maximum > 0 ? new(match.Groups["name"].Value, maximum) : null;
    }
}
