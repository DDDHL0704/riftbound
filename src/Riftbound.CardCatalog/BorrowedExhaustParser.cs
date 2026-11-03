using System.Text.RegularExpressions;

namespace Riftbound.CardCatalog;

public sealed record BorrowedExhaustSpec(int Index, string Kind, int Mana, int Targets,
    bool RequiresEmpowered, int Might = 0, int MinimumMight = 0)
{
    public int Power { get; init; }
    public IReadOnlyDictionary<string, int> TypedPower { get; init; } = new Dictionary<string, int>();
    public int Experience { get; init; }
    public bool Swift { get; init; }
    public bool RequiresSourceBattlefield { get; init; }
    public string TargetScope { get; init; } = "ANY_UNIT";
    public int Damage { get; init; }
    public string Keyword { get; init; } = "";
}

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
            var swift = cost.Contains("{{迅捷>}}", StringComparison.Ordinal);
            cost = cost.Replace("{{迅捷>}}", "", StringComparison.Ordinal);
            if (!cost.Contains("{{横置}}", StringComparison.Ordinal) && !cost.Contains("{{T}}", StringComparison.Ordinal)) continue;
            var mana = Regex.Matches(cost, @"\{\{(?<n>[0-9]+)\}\}").Sum(m => int.Parse(m.Groups["n"].Value));
            var power = Regex.Matches(cost, @"\{\{A\}\}").Count;
            var colors = new Dictionary<string, string> { ["红色"] = "red", ["绿色"] = "green", ["蓝色"] = "blue",
                ["橙色"] = "orange", ["紫色"] = "purple", ["黄色"] = "yellow" };
            var typed = colors.Select(pair => (pair.Value, Count: Regex.Matches(cost, Regex.Escape("{{" + pair.Key + "}}")).Count))
                .Where(pair => pair.Count > 0).ToDictionary(pair => pair.Value, pair => pair.Count);
            var experience = Regex.Match(cost, @"消耗(?<n>[0-9]+)经验");
            var remainder = Regex.Replace(cost, @"\{\{(?:[0-9]+|横置|T|A|红色|绿色|蓝色|橙色|紫色|黄色)\}\}|消耗[0-9]+经验|支付|[\s，,、和]", "");
            if (remainder.Length != 0) continue;
            var battlefield = parts[1].Contains("我必须位于战场中才能使用此技能", StringComparison.Ordinal);
            var effect = parts[1].Replace("我必须位于战场中才能使用此技能。", "", StringComparison.Ordinal).Trim().TrimEnd('。', '.');
            BorrowedExhaustSpec Skill(string kind, int targets = 1, int might = 0, int floor = 0,
                string scope = "ANY_UNIT", int damage = 0, string keyword = "")
                => new(ordinal, kind, mana, targets, empowered, might, floor) {
                    Power = power, TypedPower = typed, Experience = experience.Success ? int.Parse(experience.Groups["n"].Value) : 0,
                    Swift = swift, RequiresSourceBattlefield = battlefield, TargetScope = scope, Damage = damage, Keyword = keyword };
            var ready = Regex.Match(effect, @"^让(?<n>[1-9][0-9]*|一|两|二)件装备变为活跃状态$");
            if (ready.Success)
            {
                var count = ready.Groups["n"].Value;
                result.Add(Skill("READY_EQUIPMENT", count is "一" ? 1 : count is "两" or "二" ? 2 : int.Parse(count)));
                continue;
            }
            if (effect == "让一名单位变为活跃状态") { result.Add(Skill("READY_UNIT")); continue; }
            var damage = Regex.Match(effect, @"^对(?<field>战场上的)?一名单位造成(?<n>[1-9][0-9]*)点伤害$");
            if (damage.Success) {
                result.Add(Skill("DAMAGE_UNIT", scope: damage.Groups["field"].Success ? "BATTLEFIELD_UNIT" : "ANY_UNIT",
                    damage: int.Parse(damage.Groups["n"].Value))); continue;
            }
            var boon = Regex.Match(effect, @"^给予一名(?<friend>友方|休眠的友方)?单位(?:\{\{增益\}\}|增益)$");
            if (boon.Success) {
                result.Add(Skill("BOON_UNIT", scope: boon.Groups["friend"].Value switch {
                    "休眠的友方" => "EXHAUSTED_FRIENDLY_UNIT", "友方" => "FRIENDLY_UNIT", _ => "ANY_UNIT" })); continue;
            }
            var keyword = Regex.Match(effect, @"^给予一名友方单位在本回合内\{\{(?<keyword>壁垒|强攻[1-9][0-9]*|坚守[1-9][0-9]*)\}\}$");
            if (keyword.Success) {
                result.Add(Skill("KEYWORD_UNIT", scope: "FRIENDLY_UNIT", keyword: keyword.Groups["keyword"].Value)); continue;
            }
            if (effect == "{{眩晕}}一名敌方单位")
            {
                result.Add(Skill("STUN_ENEMY", scope: "ENEMY_UNIT"));
                continue;
            }
            var might = Regex.Match(effect, @"^让一名单位(?:在)?本回合内\{\{S\}\}(?<n>[+-][0-9]+)(?:[，,]\s*不得低于(?<floor>[0-9]+)\{\{S\}\})?$");
            if (might.Success)
                result.Add(Skill("MIGHT_UNIT", might: int.Parse(might.Groups["n"].Value),
                    floor: might.Groups["floor"].Success ? int.Parse(might.Groups["floor"].Value) : 0));
        }
        return result;
    }
}
