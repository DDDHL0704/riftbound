using System.Text.RegularExpressions;

namespace Riftbound.CardCatalog;

public sealed record OptionalPlayCostReductionSpec(string Category, int Mana, int Power, string SourceZone,
    bool OncePerTurn = true, bool ExhaustSource = false, int SupportingUnitCount = 0);

public static class OptionalPlayCostReductionParser
{
    public static OptionalPlayCostReductionSpec? Parse(string text)
    {
        var exhaust = Regex.Match(text,
            @"\A\s*你可以选择让我及(?<count>[0-9一二三四五六七八九十]+)名友方单位变为休眠状态，作为打出(?<category>一个法术|一件装备|一名单位)的额外费用，以此让该(?<noun>法术|装备|单位)的费用减少\{\{(?<mana>[0-9]+)\}\}。\s*\z");
        if (exhaust.Success && int.TryParse(exhaust.Groups["mana"].Value, out var amount))
        {
            var countText = exhaust.Groups["count"].Value;
            var count = int.TryParse(countText, out var numeric) ? numeric
                : countText.Length == 1 ? "一二三四五六七八九十".IndexOf(countText, StringComparison.Ordinal) + 1 : 0;
            var category = exhaust.Groups["category"].Value switch { "一件装备" => "EQUIPMENT", "一个法术" => "SPELL", _ => "UNIT" };
            if (count > 0 && exhaust.Groups["category"].Value[2..] == exhaust.Groups["noun"].Value)
                return new(category, amount, 0, "PUBLIC", OncePerTurn: false, ExhaustSource: true, SupportingUnitCount: count);
        }
        var match = Regex.Match(text,
            @"\A\s*每回合限一次，只要我位于战场上，你便可以选择减少支付\{\{(?<mana>[0-9]+)\}\}和\{\{A\}\}来打出(?<category>一件装备|一个法术|一名单位)。\s*\z");
        return match.Success && int.TryParse(match.Groups["mana"].Value, out var mana)
            ? new(match.Groups["category"].Value switch { "一件装备" => "EQUIPMENT", "一个法术" => "SPELL", _ => "UNIT" },
                mana, 1, "BATTLEFIELD") : null;
    }
}
