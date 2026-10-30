using System.Text.RegularExpressions;

namespace Riftbound.CardCatalog;

public sealed record OptionalPlayCostReductionSpec(string Category, int Mana, int Power, string SourceZone);

public static class OptionalPlayCostReductionParser
{
    public static OptionalPlayCostReductionSpec? Parse(string text)
    {
        var match = Regex.Match(text,
            @"\A\s*每回合限一次，只要我位于战场上，你便可以选择减少支付\{\{(?<mana>[0-9]+)\}\}和\{\{A\}\}来打出(?<category>一件装备|一个法术|一名单位)。\s*\z");
        return match.Success && int.TryParse(match.Groups["mana"].Value, out var mana)
            ? new(match.Groups["category"].Value switch { "一件装备" => "EQUIPMENT", "一个法术" => "SPELL", _ => "UNIT" },
                mana, 1, "BATTLEFIELD") : null;
    }
}
