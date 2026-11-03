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
    public int MinimumExperience { get; init; }
    public bool Reaction { get; init; }
    public int GeneratedMana { get; init; }
    public int? EmpoweredMana { get; init; }
    public int GeneratedPower { get; init; }
    public IReadOnlyDictionary<string, int> GeneratedTraits { get; init; } = new Dictionary<string, int>();
    public string ResourceRestriction { get; init; } = "";
    public bool RemovesSourceEmpowerment { get; init; }
    public string Conversion { get; init; } = "";
    public bool DiscardsEquipmentAsCost { get; init; }
    public bool DestroysSourceAsCost { get; init; }
    public int RecycledCardCostCount { get; init; }
    public bool RecyclesOnlyUnitsAsCost { get; init; }
    public int DrawCount { get; init; }
    public int RuneCallCount { get; init; }
    public int ScoreAmount { get; init; }
    public int InsightCount { get; init; }
    public int ExperienceGain { get; init; }
    public int? EmpoweredMight { get; init; }
    public int ExistingBoonMight { get; init; }
    public int TokenCount { get; init; }
    public string TokenName { get; init; } = "";
    public int TokenMight { get; init; }
    public string TokenKeyword { get; init; } = "";
    public bool TokensToBase { get; init; }
    public bool RequiresPlayedArmamentThisTurn { get; init; }
}

public static class BorrowedExhaustParser
{
    public static bool InheritsFriendlyExhaustAbilities(string text)
        => text.Contains("我拥有场上其他友方传奇、单位、装备卡牌的所有{{横置}}技能", StringComparison.Ordinal);

    public static IReadOnlyList<BorrowedExhaustSpec> Parse(string text)
    {
        var result = new List<BorrowedExhaustSpec>();
        var index = 0;
        var allAbilitiesRequireBattlefield = text.Contains("只有我位于战场时，才能使用我的技能", StringComparison.Ordinal);
        foreach (var line in text.Replace(@"\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var parts = Regex.Replace(line, @"（[^）]*）|\([^)]*\)", "").Trim().Split(['：', ':'], 2);
            if (parts.Length != 2) continue;
            var ordinal = index++;
            var cost = parts[0].Trim();
            var removesEmpowerment = Regex.IsMatch(cost, "解除(?:我的|此牌的)强化");
            cost = Regex.Replace(cost, "解除(?:我的|此牌的)强化", "");
            var discardsEquipment = cost.Contains("弃置一张装备牌", StringComparison.Ordinal);
            cost = cost.Replace("弃置一张装备牌", "", StringComparison.Ordinal);
            var recycle = Regex.Match(cost, @"从(?:你的)?废牌堆中回收(?<count>一|二|两|三|[1-9][0-9]*)(?<type>张牌|名单位)");
            var recycleCount = recycle.Success ? recycle.Groups["count"].Value switch {
                "一" => 1, "二" or "两" => 2, "三" => 3, var n => int.Parse(n) } : 0;
            if (recycle.Success) cost = cost.Remove(recycle.Index, recycle.Length);
            var destroysSource = cost.Contains("摧毁此牌", StringComparison.Ordinal)
                || cost.Contains("摧毁我", StringComparison.Ordinal);
            cost = cost.Replace("摧毁此牌", "", StringComparison.Ordinal)
                .Replace("摧毁我", "", StringComparison.Ordinal);
            var level = Regex.Match(cost, @"\{\{等级(?<n>[0-9]+)>\}\}");
            cost = Regex.Replace(cost, @"\{\{等级[0-9]+>\}\}|\{\{>>\}\}", "");
            var reaction = cost.Contains("{{反应>}}", StringComparison.Ordinal);
            cost = cost.Replace("{{反应>}}", "", StringComparison.Ordinal);
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
            var battlefield = allAbilitiesRequireBattlefield || Regex.IsMatch(parts[1], "我必须位于战场[中上]才能使用此技能");
            var armament = parts[1].Contains("只能在你本回合打出了一件武装后使用", StringComparison.Ordinal);
            var effect = Regex.Replace(parts[1], "我必须位于战场[中上]才能使用此技能。?|只能在你本回合打出了一件武装后使用。?", "")
                .Trim().TrimEnd('。', '.');
            BorrowedExhaustSpec Skill(string kind, int targets = 1, int might = 0, int floor = 0,
                string scope = "ANY_UNIT", int damage = 0, string keyword = "")
                => new(ordinal, kind, mana, targets, empowered, might, floor) {
                    Power = power, TypedPower = typed, Experience = experience.Success ? int.Parse(experience.Groups["n"].Value) : 0,
                    Swift = swift, RequiresSourceBattlefield = battlefield, TargetScope = scope, Damage = damage, Keyword = keyword,
                    MinimumExperience = level.Success ? int.Parse(level.Groups["n"].Value) : 0, Reaction = reaction,
                    RemovesSourceEmpowerment = removesEmpowerment, DiscardsEquipmentAsCost = discardsEquipment,
                    DestroysSourceAsCost = destroysSource, RequiresPlayedArmamentThisTurn = armament,
                    RecycledCardCostCount = recycleCount, RecyclesOnlyUnitsAsCost = recycle.Groups["type"].Value == "名单位" };
            var conversion = Regex.Match(effect, @"^\{\{反应\}\}\s*[—-]\s*支付任意数量的(?<input>法力|\{\{A\}\})来\{\{获得\}\}等量的(?<output>法力|\{\{A\}\})$");
            if (conversion.Success && conversion.Groups["input"].Value != conversion.Groups["output"].Value)
            {
                result.Add(Skill("RESOURCE", 0) with { Reaction = true,
                    Conversion = conversion.Groups["input"].Value == "法力" ? "MANA_TO_POWER" : "POWER_TO_MANA" });
                continue;
            }
            var resource = Regex.Match(effect, @"^(?:\{\{反应\}\}\s*[—-]\s*)?\{\{获得\}\}\s*(?<gain>(?:\{\{(?:[0-9]+|A|红色|绿色|蓝色|橙色|紫色|黄色)\}\}|和|\s)+)(?<restriction>[，,]但仅可用于打出法术|[，,]仅可用于打出装备或使用装备技能|。此法力仅能用于打出法术|。此法力仅能用于打出单位或单位的主动技能|。此法力仅可在法术对决期间消耗|，用以支付符能费用)?(?:。如果此牌\{\{已强化\}\}，则改为\{\{获得\}\}\{\{(?<empowered>[0-9]+)\}\})?$");
            if (resource.Success && (reaction || effect.StartsWith("{{反应}}", StringComparison.Ordinal)))
            {
                var gain = resource.Groups["gain"].Value;
                var restriction = resource.Groups["restriction"].Value;
                result.Add(Skill("RESOURCE", 0) with {
                    Reaction = true,
                    GeneratedMana = Regex.Matches(gain, @"\{\{(?<n>[0-9]+)\}\}").Sum(m => int.Parse(m.Groups["n"].Value)),
                    GeneratedPower = Regex.Matches(gain, @"\{\{A\}\}").Count,
                    GeneratedTraits = colors.Select(pair => (pair.Value, Count: Regex.Matches(gain, Regex.Escape("{{" + pair.Key + "}}")).Count))
                        .Where(pair => pair.Count > 0).ToDictionary(pair => pair.Value, pair => pair.Count),
                    EmpoweredMana = resource.Groups["empowered"].Success ? int.Parse(resource.Groups["empowered"].Value) : null,
                    ResourceRestriction = restriction.Contains("法术对决", StringComparison.Ordinal) ? "DUEL"
                        : restriction.Contains("单位", StringComparison.Ordinal) ? "UNIT"
                        : restriction.Contains("装备", StringComparison.Ordinal) ? "EQUIPMENT"
                        : restriction.Contains("法术", StringComparison.Ordinal) ? "SPELL" : "" });
                continue;
            }
            // Reaction non-resource effects need their own timing and templates.
            if (reaction) continue;
            var unitToken = Regex.Match(effect, @"^打出(?<count>一|二|两|三|[1-9][0-9]*)名(?<might>[1-9][0-9]*)\{\{S\}\}的“(?<name>随从|机器人|黄沙士兵|战鹰)”(?<base>到你的基地)?(?:，它们?拥有\{\{(?<keyword>法盾)\}\})?$");
            if (unitToken.Success)
            {
                var count = unitToken.Groups["count"].Value switch { "一" => 1, "二" or "两" => 2, "三" => 3, var n => int.Parse(n) };
                result.Add(Skill("CREATE_UNIT_TOKEN", 0) with { TokenCount = count,
                    TokenName = unitToken.Groups["name"].Value, TokenMight = int.Parse(unitToken.Groups["might"].Value),
                    TokenKeyword = unitToken.Groups["keyword"].Value, TokensToBase = unitToken.Groups["base"].Success });
                continue;
            }
            var insight = Regex.Match(effect, @"^\{\{洞察(?<count>[1-9][0-9]*)\}\}，然后抽一张牌。获得(?<experience>[1-9][0-9]*)经验$");
            if (insight.Success)
            {
                result.Add(Skill("INSIGHT_DRAW_EXPERIENCE", 0) with { InsightCount = int.Parse(insight.Groups["count"].Value),
                    DrawCount = 1, ExperienceGain = int.Parse(insight.Groups["experience"].Value) });
                continue;
            }
            if (effect == "打出一个休眠的“金币”装备指示物")
            {
                result.Add(Skill("CREATE_GOLD", 0));
                continue;
            }
            var score = Regex.Match(effect, @"^获得(?<n>[1-9][0-9]*)分$");
            if (score.Success)
            {
                result.Add(Skill("SCORE", 0) with { ScoreAmount = int.Parse(score.Groups["n"].Value) });
                continue;
            }
            if (effect is "摧毁此牌" or "摧毁我")
            {
                result.Add(Skill("DESTROY_SELF", 0));
                continue;
            }
            if (effect is "给予我增益" or "给予我{{增益}}")
            {
                result.Add(Skill("BOON_SELF", 0));
                continue;
            }
            if (effect == "强化另一件装备")
            {
                result.Add(Skill("EMPOWER_EQUIPMENT"));
                continue;
            }
            var empoweredMight = Regex.Match(effect, @"^给予一名单位在本回合内\{\{S\}\}(?<base>\+[1-9][0-9]*)。如果此牌\{\{已强化\}\}，则改为给予该单位在本回合内\{\{S\}\}(?<empowered>\+[1-9][0-9]*)$");
            if (empoweredMight.Success)
            {
                result.Add(Skill("MIGHT_UNIT", might: int.Parse(empoweredMight.Groups["base"].Value)) with {
                    EmpoweredMight = int.Parse(empoweredMight.Groups["empowered"].Value) });
                continue;
            }
            var boonMight = Regex.Match(effect, @"^给予一名单位\{\{增益\}\}。如果该单位已拥有增益，则改为给予其在本回合内\{\{S\}\}(?<might>\+[1-9][0-9]*)$");
            if (boonMight.Success)
            {
                result.Add(Skill("BOON_OR_MIGHT") with { ExistingBoonMight = int.Parse(boonMight.Groups["might"].Value) });
                continue;
            }
            if (effect == "弃置一张手牌，然后抽一张牌")
            {
                result.Add(Skill("DISCARD_DRAW", 0) with { DrawCount = 1 });
                continue;
            }
            var draw = Regex.Match(effect, @"^抽(?<n>一|二|两|[1-9][0-9]*)张牌(?:并召出(?<runes>一|二|两|[1-9][0-9]*)枚休眠的符文)?$");
            if (draw.Success)
            {
                var count = draw.Groups["n"].Value switch { "一" => 1, "二" or "两" => 2, _ => int.Parse(draw.Groups["n"].Value) };
                var runes = draw.Groups["runes"].Value switch { "" => 0, "一" => 1, "二" or "两" => 2, var value => int.Parse(value) };
                result.Add(Skill("DRAW", 0) with { DrawCount = count, RuneCallCount = runes });
                continue;
            }
            var ready = Regex.Match(effect, @"^让(?<n>[1-9][0-9]*|一|两|二)件装备变为活跃状态$");
            if (ready.Success)
            {
                var count = ready.Groups["n"].Value;
                result.Add(Skill("READY_EQUIPMENT", count is "一" ? 1 : count is "两" or "二" ? 2 : int.Parse(count)));
                continue;
            }
            if (effect == "让一名单位变为活跃状态") { result.Add(Skill("READY_UNIT")); continue; }
            if (effect == "让一名单位本回合内获得{{游走}}") { result.Add(Skill("ROAM_UNIT")); continue; }
            var moveToBase = Regex.Match(effect, @"^将战场上(?:的)?一名(?<exhausted>处于休眠状态的)?友方单位移动到其(?:所属的)?基地$");
            if (moveToBase.Success)
            {
                result.Add(Skill("MOVE_TO_BASE", scope: moveToBase.Groups["exhausted"].Success
                    ? "EXHAUSTED_FRIENDLY_BATTLEFIELD_UNIT" : "FRIENDLY_BATTLEFIELD_UNIT"));
                continue;
            }
            if (effect == "在战场和其所属基地之间移动一名友方单位")
            {
                result.Add(Skill("MOVE_BETWEEN_BASE_AND_BATTLEFIELD", scope: "FRIENDLY_UNIT"));
                continue;
            }
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
            var keyword = Regex.Match(effect, @"^给予一名(?<friend>友方)?单位在本回合内\{\{(?<keyword>壁垒|强攻[1-9][0-9]*|坚守[1-9][0-9]*)\}\}$");
            if (keyword.Success) {
                result.Add(Skill("KEYWORD_UNIT", scope: keyword.Groups["friend"].Success ? "FRIENDLY_UNIT" : "ANY_UNIT", keyword: keyword.Groups["keyword"].Value)); continue;
            }
            if (effect == "{{眩晕}}一名敌方单位")
            {
                result.Add(Skill("STUN_ENEMY", scope: "ENEMY_UNIT"));
                continue;
            }
            var might = Regex.Match(effect, @"^(?:让|给予)(?<field>战场上的)?一名单位(?:在)?本回合内\{\{S\}\}(?<n>[+-][0-9]+)(?:[，,]\s*不得低于(?<floor>[0-9]+)\{\{S\}\})?$");
            if (might.Success)
                result.Add(Skill("MIGHT_UNIT", might: int.Parse(might.Groups["n"].Value),
                    floor: might.Groups["floor"].Success ? int.Parse(might.Groups["floor"].Value) : 0,
                    scope: might.Groups["field"].Success ? "BATTLEFIELD_UNIT" : "ANY_UNIT"));
        }
        return result;
    }
}
