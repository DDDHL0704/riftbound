using System.Text.RegularExpressions;

namespace Riftbound.CardCatalog;

public sealed record BorrowedExhaustSpec(int Index, string Kind, int Mana, int Targets,
    bool RequiresEmpowered, int Might = 0, int MinimumMight = 0);

public static class BorrowedExhaustParser
{
    public static bool InheritsFriendlyExhaustAbilities(string text)
        => text.Contains("我拥有场上其他友方传奇、单位、装备卡牌的所有{{横置}}技能", StringComparison.Ordinal);

    public static IReadOnlyList<BorrowedExhaustSpec> Parse(string text)
    {
        var result = new List<BorrowedExhaustSpec>();
        var index = 0;
        foreach (var line in text.Replace(@"\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var parts = Regex.Replace(line, @"（[^）]*）|\([^)]*\)", "").Trim().Split(['：', ':'], 2);
            if (parts.Length != 2) continue;
            var ordinal = index++;
            var cost = parts[0].Trim();
            var empowered = cost.Contains("{{已强化>}}", StringComparison.Ordinal);
            cost = cost.Replace("{{已强化>}}", "", StringComparison.Ordinal);
            if (!cost.Contains("{{横置}}", StringComparison.Ordinal) && !cost.Contains("{{T}}", StringComparison.Ordinal)) continue;
            var mana = Regex.Matches(cost, @"\{\{(?<n>[0-9]+)\}\}").Sum(m => int.Parse(m.Groups["n"].Value));
            var remainder = Regex.Replace(cost, @"\{\{(?:[0-9]+|横置|T)\}\}|支付|[\s，,、和]", "");
            if (remainder.Length != 0) continue;
            var effect = parts[1].Trim().TrimEnd('。', '.');
            var ready = Regex.Match(effect, @"^让(?<n>[1-9][0-9]*|一|两|二)件装备变为活跃状态$");
            if (ready.Success)
            {
                var count = ready.Groups["n"].Value;
                result.Add(new(ordinal, "READY_EQUIPMENT", mana, count is "一" ? 1 : count is "两" or "二" ? 2 : int.Parse(count), empowered));
                continue;
            }
            if (effect == "{{眩晕}}一名敌方单位")
            {
                result.Add(new(ordinal, "STUN_ENEMY", mana, 1, empowered));
                continue;
            }
            var might = Regex.Match(effect, @"^让一名单位(?:在)?本回合内\{\{S\}\}(?<n>[+-][0-9]+)(?:[，,]\s*不得低于(?<floor>[0-9]+)\{\{S\}\})?$");
            if (might.Success)
                result.Add(new(ordinal, "MIGHT_UNIT", mana, 1, empowered, int.Parse(might.Groups["n"].Value),
                    might.Groups["floor"].Success ? int.Parse(might.Groups["floor"].Value) : 0));
        }
        return result;
    }
}
