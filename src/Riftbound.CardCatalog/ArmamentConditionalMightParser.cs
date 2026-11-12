using System.Text.RegularExpressions;

namespace Riftbound.CardCatalog;

public sealed record OfficialArmamentConditionalMight(int Amount, string Condition, int Threshold = 0);

// Candidate only: derive lower-box rules from the reviewed official source,
// with no per-card number table or additional hand-authored catalog fields.
internal static class ArmamentConditionalMightParser
{
    internal static OfficialArmamentConditionalMight? Parse(string text)
    {
        var plain = Regex.Replace(Regex.Replace(text, @"（[^）]*）", ""), @"\s+", "");
        var attached = Regex.Match(plain, @"^如果此牌是在本回合贴附到我身上的，则我额外获得\{\{S\}\}\+(?<amount>[1-9][0-9]*)。$");
        if (attached.Success && int.TryParse(attached.Groups["amount"].Value, out var amount))
            return new(amount, "ATTACHED_THIS_TURN");
        var level = Regex.Match(plain, @"^\{\{等级(?<level>[1-9][0-9]*)\}\}我额外获得\{\{S\}\}\+(?<amount>[1-9][0-9]*)。$");
        if (level.Success && int.TryParse(level.Groups["amount"].Value, out amount)
            && int.TryParse(level.Groups["level"].Value, out var threshold))
            return new(amount, "CONTROLLER_EXPERIENCE_AT_LEAST", threshold);
        var here = Regex.Match(plain, @"^如果我所在的战场上受你控制的其他单位有且仅有一个，则我获得\{\{S\}\}\+(?<amount>[1-9][0-9]*)。$");
        return here.Success && int.TryParse(here.Groups["amount"].Value, out amount)
            ? new(amount, "EXACT_OTHER_FRIENDLY_UNITS_HERE", 1) : null;
    }
}
