using System.Text.RegularExpressions;

namespace Riftbound.CardCatalog;

public sealed record EmpowerCostSpec(int Mana, int Power, IReadOnlyDictionary<string, int> TypedPower,
    bool Exhaust, string AdditionalCost, bool SupportedResources);
public sealed record EmpowerSpec(int Index, IReadOnlyList<EmpowerCostSpec> Alternatives,
    int ManaReductionAtMostRunes = 0, int? RuneLimit = null, bool ReduceManaPerRune = false);
public sealed record EmpoweredSelfSpec(int Might, IReadOnlyList<string> Keywords);
public sealed record EmpoweredSpellSpec(bool PreventsCounter, int AdditionalChosenNegativeMight);
public sealed record EmpoweredSpellCostReductionSpec(int Mana, int GenericPower, int MinimumMana);

public static class EmpowerParser
{
    private static readonly Dictionary<string, string> Colors = new() {
        ["红色"] = "red", ["绿色"] = "green", ["蓝色"] = "blue", ["紫色"] = "purple", ["橙色"] = "orange", ["黄色"] = "yellow"
    };

    public static IReadOnlyList<EmpowerSpec> Parse(string text)
    {
        var matches = Regex.Matches(text, @"\{\{强化(?<cost>[^}]*)\}\}(?<tail>[^\r\n]*)");
        var result = new List<EmpowerSpec>();
        foreach (Match match in matches) {
            var start = text.LastIndexOf('\n', Math.Max(0, match.Index - 1)) + 1;
            if (!string.IsNullOrWhiteSpace(text[start..match.Index])) continue;
            var token = match.Groups["cost"].Value;
            var tail = match.Groups["tail"].Value;
            string[] alternatives;
            if (token.Length > 0) alternatives = [token];
            else {
                var explicitCost = tail.Split('（', '(')[0].Trim(' ', '—', '-', '。');
                alternatives = explicitCost.Split('或').Select(c => c.Trim()).ToArray();
            }
            var costs = alternatives.Select(ParseCost).ToArray();
            var limited = Regex.Match(tail, @"符文数量不超过(?<n>[零一二三四五六七八九0-9]+)枚.*?费用减少\{\{(?<amount>[0-9]+)\}\}");
            result.Add(new(result.Count, costs,
                limited.Success ? int.Parse(limited.Groups["amount"].Value) : 0,
                limited.Success ? Integer(limited.Groups["n"].Value) : null,
                Regex.IsMatch(tail, @"每控制一枚符文.*?费用.*?减少\{\{1\}\}")));
        }
        return result;
    }

    private static EmpowerCostSpec ParseCost(string cost)
    {
        var missing = string.IsNullOrWhiteSpace(cost);
        cost = cost.Replace("{{", "").Replace("}}", "").Replace("支付", "").Trim(' ', '，', '。');
        var exhaust = cost.Contains("横置", StringComparison.Ordinal) || cost.Contains('T');
        var nonresource = cost.Contains("弃置", StringComparison.Ordinal) ? cost
            : cost.Contains("摧毁", StringComparison.Ordinal) ? cost : "";
        var typed = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (word, trait) in Colors) {
            var count = Regex.Matches(cost, Regex.Escape(word)).Count;
            if (count > 0) typed[trait] = count;
        }
        var number = Regex.Match(cost, @"^[0-9]+");
        var mana = nonresource.Length == 0 && number.Success ? int.Parse(number.Value) : 0;
        var power = nonresource.Length == 0 ? cost.Count(c => c == 'A') : 0;
        var remaining = Regex.Replace(cost, @"^[0-9]+", "");
        foreach (var word in Colors.Keys) remaining = remaining.Replace(word, "");
        remaining = remaining.Replace("A", "").Replace("T", "").Replace("横置", "").Trim(' ', '，', ',');
        return new(mana, power, typed, exhaust, nonresource, !missing && nonresource.Length == 0 && remaining.Length == 0);
    }

    public static EmpoweredSelfSpec Self(string text)
    {
        var might = 0; var keywords = new List<string>();
        foreach (Match clause in Regex.Matches(text, @"\{\{已强化>\}\}\s*我获得(?<body>[^\r\n。（(]*)")) {
            var body = clause.Groups["body"].Value;
            var amount = Regex.Match(body, @"\{\{S\}\}\+(?<amount>[0-9]+)");
            if (amount.Success) might += int.Parse(amount.Groups["amount"].Value);
            foreach (Match keyword in Regex.Matches(body, @"\{\{(?<keyword>法盾|强攻|坚守|游走|壁垒)(?<n>[0-9]*)\}\}"))
                keywords.Add(keyword.Groups["keyword"].Value + keyword.Groups["n"].Value);
        }
        return new(might, keywords);
    }

    public static IReadOnlyList<EmpoweredSpellSpec> SpellEffects(string text)
    {
        var result = new List<EmpoweredSpellSpec>();
        foreach (Match clause in Regex.Matches(text, @"\{\{已强化>\}\}(?<body>[^\r\n]*)")) {
            var body = clause.Groups["body"].Value;
            var counter = body.Contains("你的法术和技能无法被无效化", StringComparison.Ordinal);
            var negative = Regex.Match(body, @"你控制的法术或技能将给予其选作目标的一名单位-\{\{S\}\}.*?额外给予\{\{S\}\}-(?<n>[0-9]+)");
            if (counter || negative.Success) result.Add(new(counter, negative.Success ? int.Parse(negative.Groups["n"].Value) : 0));
        }
        return result;
    }

    public static IReadOnlyList<EmpoweredSpellCostReductionSpec> SpellCostReductions(string text)
        => Regex.Matches(text,
                @"(?m)^\s*\{\{已强化>\}\}\s*你的法术费用减少\{\{(?<mana>[0-9]+)\}\}和\{\{A\}\}，不得低于\{\{(?<floor>[0-9]+)\}\}。\s*$")
            .Select(match => new EmpoweredSpellCostReductionSpec(
                int.Parse(match.Groups["mana"].Value), 1, int.Parse(match.Groups["floor"].Value))).ToArray();

    private static int Integer(string value) => int.TryParse(value, out var n) ? n : value switch {
        "零" => 0, "一" => 1, "二" => 2, "三" => 3, "四" => 4, "五" => 5, "六" => 6, "七" => 7, "八" => 8, "九" => 9,
        _ => throw new InvalidOperationException("Unknown printed numeric threshold.")
    };
}
