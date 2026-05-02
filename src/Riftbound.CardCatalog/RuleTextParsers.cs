using System.Text.RegularExpressions;
using Riftbound.Contracts;

namespace Riftbound.CardCatalog;

public sealed record RuleTextParseResult(
    ParsedCostSpec Cost,
    IReadOnlyList<KeywordSpec> Keywords,
    IReadOnlyList<TargetSpec> Targets,
    IReadOnlyList<TriggerSpec> Triggers,
    IReadOnlyList<ReplacementSpec> Replacements,
    IReadOnlyList<ActivatedAbilitySpec> ActivatedAbilities,
    IReadOnlyList<StaticAbilitySpec> StaticAbilities,
    IReadOnlyList<EffectPhraseSpec> Effects);

public static class RuleTextParser
{
    public static RuleTextParseResult Parse(OfficialCard card)
    {
        var text = card.CardEffect ?? string.Empty;
        var keywords = KeywordParser.Parse(text, card.Tag);
        var effects = EffectPhraseParser.Parse(text);
        return new RuleTextParseResult(
            CostParser.Parse(card, keywords),
            keywords,
            TargetParser.Parse(text),
            TriggerParser.Parse(text),
            ReplacementParser.Parse(text),
            ActivatedAbilityParser.Parse(text),
            StaticAbilityParser.Parse(text, keywords),
            effects);
    }
}

public static partial class KeywordParser
{
    private static readonly string[] KnownKeywords =
    [
        "迅捷",
        "反应",
        "急速",
        "强攻",
        "坚守",
        "壁垒",
        "后排",
        "游走",
        "瞬息",
        "绝念",
        "预知",
        "狩猎",
        "等级",
        "鼓舞",
        "法盾",
        "待命",
        "回响",
        "伏击",
        "装配",
        "灵便",
        "百炼",
        "增益"
    ];

    public static IReadOnlyList<KeywordSpec> Parse(string text, string tag)
    {
        var results = new List<KeywordSpec>();
        var haystacks = new[] { text ?? string.Empty, tag ?? string.Empty };
        foreach (var haystack in haystacks)
        {
            foreach (Match match in BracedTokenRegex().Matches(haystack))
            {
                AddKeyword(results, match.Groups["token"].Value);
            }

            foreach (var keyword in KnownKeywords)
            {
                if (haystack.Contains(keyword, StringComparison.Ordinal))
                {
                    AddKeyword(results, keyword);
                }
            }
        }

        return results
            .GroupBy(keyword => $"{keyword.Keyword}\n{keyword.RawText}", StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(keyword => keyword.Keyword, StringComparer.Ordinal)
            .ThenBy(keyword => keyword.RawText, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddKeyword(List<KeywordSpec> results, string rawToken)
    {
        var normalized = rawToken
            .Replace(">", string.Empty, StringComparison.Ordinal)
            .Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        foreach (var keyword in KnownKeywords)
        {
            if (!normalized.StartsWith(keyword, StringComparison.Ordinal))
            {
                continue;
            }

            var value = normalized[keyword.Length..].Trim();
            results.Add(new KeywordSpec(
                keyword,
                normalized,
                string.IsNullOrWhiteSpace(value) ? null : value));
            return;
        }
    }

    [GeneratedRegex(@"\{\{(?<token>[^}]+)\}\}")]
    private static partial Regex BracedTokenRegex();
}

public static partial class CostParser
{
    public static ParsedCostSpec Parse(OfficialCard card, IReadOnlyList<KeywordSpec> keywords)
    {
        var text = card.CardEffect ?? string.Empty;
        var additionalCosts = new List<string>();
        var optionalCosts = new List<string>();

        foreach (var keyword in keywords.Where(keyword => string.Equals(keyword.Keyword, "回响", StringComparison.Ordinal)))
        {
            optionalCosts.Add(string.IsNullOrWhiteSpace(keyword.Value)
                ? "echo"
                : $"echo:{keyword.Value}");
        }

        foreach (Match match in ExtraPayRegex().Matches(text))
        {
            optionalCosts.Add($"extra-pay:{match.Groups["cost"].Value.Trim()}");
        }

        if (text.Contains("额外费用", StringComparison.Ordinal))
        {
            optionalCosts.Add("optional-additional-cost");
        }

        if (text.Contains("{{横置}}", StringComparison.Ordinal))
        {
            additionalCosts.Add("exhaust");
        }

        if (text.Contains("摧毁此牌", StringComparison.Ordinal))
        {
            additionalCosts.Add("destroy-this-card");
        }

        if (text.Contains("弃置", StringComparison.Ordinal))
        {
            additionalCosts.Add("discard-card");
        }

        foreach (Match match in ExperiencePayRegex().Matches(text))
        {
            optionalCosts.Add($"experience:{match.Groups["amount"].Value.Trim()}");
        }

        return new ParsedCostSpec(
            card.Energy,
            card.ReturnEnergy,
            card.Power,
            additionalCosts.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            optionalCosts.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
    }

    [GeneratedRegex(@"额外支付\{\{(?<cost>[^}]+)\}\}")]
    private static partial Regex ExtraPayRegex();

    [GeneratedRegex(@"支付(?<amount>[0-9一二三四五六七八九十]+)点?经验")]
    private static partial Regex ExperiencePayRegex();
}

public static partial class TargetParser
{
    public static IReadOnlyList<TargetSpec> Parse(string text)
    {
        return SplitRulesText(text)
            .Where(segment => segment.Contains("选择", StringComparison.Ordinal)
                || segment.Contains("一名", StringComparison.Ordinal)
                || segment.Contains("一件", StringComparison.Ordinal)
                || segment.Contains("一个", StringComparison.Ordinal)
                || segment.Contains("所有", StringComparison.Ordinal)
                || segment.Contains("至多", StringComparison.Ordinal)
                || segment.Contains("最多", StringComparison.Ordinal))
            .Select(ToTargetSpec)
            .GroupBy(target => $"{target.Scope}\n{target.Text}", StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    private static TargetSpec ToTargetSpec(string segment)
    {
        var scope = DetermineScope(segment);
        var optional = segment.Contains("可以选择", StringComparison.Ordinal)
            || segment.Contains("至多", StringComparison.Ordinal)
            || segment.Contains("最多", StringComparison.Ordinal);
        var max = DetermineCount(segment);
        var min = optional ? 0 : Math.Min(1, max ?? 1);
        if (segment.Contains("所有", StringComparison.Ordinal))
        {
            min = 0;
            max = null;
        }

        return new TargetSpec(scope, min, max, segment, optional);
    }

    private static string DetermineScope(string segment)
    {
        if (segment.Contains("法术", StringComparison.Ordinal))
        {
            return "stack-spell";
        }

        if (segment.Contains("装备", StringComparison.Ordinal))
        {
            return "equipment";
        }

        if (segment.Contains("手牌", StringComparison.Ordinal))
        {
            return "hand-card";
        }

        if (segment.Contains("废牌堆", StringComparison.Ordinal))
        {
            return "graveyard-card";
        }

        if (segment.Contains("主牌堆", StringComparison.Ordinal))
        {
            return "main-deck-card";
        }

        if (segment.Contains("战场", StringComparison.Ordinal))
        {
            return segment.Contains("单位", StringComparison.Ordinal) ? "battlefield-unit" : "battlefield-object";
        }

        if (segment.Contains("单位", StringComparison.Ordinal))
        {
            return "unit";
        }

        if (segment.Contains("玩家", StringComparison.Ordinal))
        {
            return "player";
        }

        return "object";
    }

    private static int? DetermineCount(string segment)
    {
        if (segment.Contains("所有", StringComparison.Ordinal))
        {
            return null;
        }

        var match = CountRegex().Match(segment);
        if (!match.Success)
        {
            return 1;
        }

        return ParseChineseNumber(match.Groups["count"].Value);
    }

    private static int ParseChineseNumber(string raw)
    {
        if (int.TryParse(raw, out var numeric))
        {
            return numeric;
        }

        return raw switch
        {
            "一" => 1,
            "两" => 2,
            "二" => 2,
            "三" => 3,
            "四" => 4,
            "五" => 5,
            "六" => 6,
            "七" => 7,
            "八" => 8,
            "九" => 9,
            "十" => 10,
            _ => 1
        };
    }

    [GeneratedRegex(@"(?:至多|最多)?(?<count>[0-9一两二三四五六七八九十]+)(?:名|件|个|张|枚)")]
    private static partial Regex CountRegex();

    internal static IReadOnlyList<string> SplitRulesText(string? text)
    {
        return (text ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split(['\n', '。', '；', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToArray();
    }
}

public static class TriggerParser
{
    public static IReadOnlyList<TriggerSpec> Parse(string text)
    {
        return TargetParser.SplitRulesText(text)
            .Where(segment => segment.Contains("当", StringComparison.Ordinal)
                || segment.Contains("每当", StringComparison.Ordinal)
                || segment.Contains("打出我时", StringComparison.Ordinal)
                || segment.Contains("回合开始", StringComparison.Ordinal)
                || segment.Contains("被摧毁", StringComparison.Ordinal)
                || segment.Contains("征服", StringComparison.Ordinal))
            .Select(segment => new TriggerSpec(
                DetermineKind(segment),
                DetermineTiming(segment),
                segment,
                "Parsed trigger candidate; queue ordering remains a later rule-domain implementation."))
            .ToArray();
    }

    private static string DetermineKind(string segment)
    {
        if (segment.Contains("打出", StringComparison.Ordinal))
        {
            return "on-play";
        }

        if (segment.Contains("回合开始", StringComparison.Ordinal))
        {
            return "turn-start";
        }

        if (segment.Contains("被摧毁", StringComparison.Ordinal))
        {
            return "destroyed";
        }

        if (segment.Contains("征服", StringComparison.Ordinal))
        {
            return "conquer";
        }

        return "triggered";
    }

    private static string DetermineTiming(string segment)
    {
        if (segment.Contains("打出", StringComparison.Ordinal))
        {
            return "play-resolution";
        }

        if (segment.Contains("回合开始", StringComparison.Ordinal))
        {
            return "turn-start";
        }

        return "unspecified";
    }
}

public static class ReplacementParser
{
    public static IReadOnlyList<ReplacementSpec> Parse(string text)
    {
        return TargetParser.SplitRulesText(text)
            .Where(segment => segment.Contains("改为", StringComparison.Ordinal)
                || segment.Contains("防止", StringComparison.Ordinal)
                || segment.Contains("替代", StringComparison.Ordinal)
                || segment.Contains("无效化本回合内所有", StringComparison.Ordinal))
            .Select(segment => new ReplacementSpec(
                DetermineKind(segment),
                DetermineAppliesTo(segment),
                segment,
                "Parsed replacement/prevention candidate; replacement pass ordering is not executed by the P3 skeleton."))
            .ToArray();
    }

    private static string DetermineKind(string segment)
    {
        if (segment.Contains("防止", StringComparison.Ordinal)
            || segment.Contains("无效化", StringComparison.Ordinal))
        {
            return "prevention";
        }

        return "replacement";
    }

    private static string DetermineAppliesTo(string segment)
    {
        if (segment.Contains("伤害", StringComparison.Ordinal))
        {
            return "damage";
        }

        return "effect";
    }
}

public static class ActivatedAbilityParser
{
    public static IReadOnlyList<ActivatedAbilitySpec> Parse(string text)
    {
        return TargetParser.SplitRulesText(text)
            .Where(segment => segment.Contains("：", StringComparison.Ordinal)
                || segment.Contains(":", StringComparison.Ordinal))
            .Select(segment =>
            {
                var parts = segment.Split(['：', ':'], 2, StringSplitOptions.TrimEntries);
                var cost = parts.Length > 0 ? parts[0] : string.Empty;
                var effect = parts.Length > 1 ? parts[1] : string.Empty;
                return new ActivatedAbilitySpec(
                    cost,
                    effect,
                    EffectPhraseParser.ParseTemplateIds(effect),
                    BehaviorImplementationStatuses.Unimplemented,
                    "Activated ability parsed for P3 routing only; execution remains unimplemented.");
            })
            .ToArray();
    }
}

public static class StaticAbilityParser
{
    public static IReadOnlyList<StaticAbilitySpec> Parse(
        string text,
        IReadOnlyList<KeywordSpec> keywords)
    {
        var staticSpecs = new List<StaticAbilitySpec>();
        foreach (var keyword in keywords)
        {
            staticSpecs.Add(new StaticAbilitySpec(
                "keyword",
                keyword.RawText,
                BehaviorImplementationStatuses.Unimplemented,
                "Keyword/static ability parsed for status display; full rule execution is handled by later rule domains or existing P2 mappings."));
        }

        foreach (var segment in TargetParser.SplitRulesText(text)
            .Where(segment => segment.Contains("不能", StringComparison.Ordinal)
                || segment.Contains("可以从", StringComparison.Ordinal)))
        {
            staticSpecs.Add(new StaticAbilitySpec(
                "continuous-text",
                segment,
                BehaviorImplementationStatuses.Unimplemented,
                "Continuous text parsed for status display; enforcement is outside the P3 skeleton."));
        }

        return staticSpecs
            .GroupBy(spec => $"{spec.Kind}\n{spec.Text}", StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }
}

public static class EffectPhraseParser
{
    public static IReadOnlyList<EffectPhraseSpec> Parse(string text)
    {
        return ParseTemplateIds(text)
            .Select(templateId => new EffectPhraseSpec(
                templateId,
                FirstPhraseForTemplate(text, templateId),
                BehaviorImplementationStatuses.Unimplemented,
                "Template parser candidate; execution is skeleton-only until explicitly mapped."))
            .ToArray();
    }

    public static IReadOnlyList<string> ParseTemplateIds(string text)
    {
        var templateIds = new List<string>();
        AddIf(templateIds, text, BehaviorTemplateIds.Draw, "抽");
        AddIf(templateIds, text, BehaviorTemplateIds.Damage, "伤害");
        AddIf(templateIds, text, BehaviorTemplateIds.Destroy, "摧毁");
        AddIf(templateIds, text, BehaviorTemplateIds.Move, "移动");
        AddIf(templateIds, text, BehaviorTemplateIds.Recall, "返回", "召回");
        AddIf(templateIds, text, BehaviorTemplateIds.Recycle, "回收");
        AddIf(templateIds, text, BehaviorTemplateIds.Banish, "放逐");
        AddIf(templateIds, text, BehaviorTemplateIds.Stun, "眩晕");
        AddIf(templateIds, text, BehaviorTemplateIds.TempMight, "{{S}}+", "{{S}}-", "战力");
        AddIf(templateIds, text, BehaviorTemplateIds.Boon, "增益");
        AddIf(templateIds, text, BehaviorTemplateIds.GainExperience, "经验");
        AddIf(templateIds, text, BehaviorTemplateIds.Assemble, "装配", "百炼");
        AddIf(templateIds, text, BehaviorTemplateIds.Echo, "回响");
        AddIf(templateIds, text, BehaviorTemplateIds.Ambush, "伏击");
        return templateIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    private static void AddIf(
        List<string> templateIds,
        string text,
        string templateId,
        params string[] needles)
    {
        if (needles.Any(needle => (text ?? string.Empty).Contains(needle, StringComparison.Ordinal)))
        {
            templateIds.Add(templateId);
        }
    }

    private static string FirstPhraseForTemplate(string text, string templateId)
    {
        string[] needles = templateId switch
        {
            BehaviorTemplateIds.Draw => ["抽"],
            BehaviorTemplateIds.Damage => ["伤害"],
            BehaviorTemplateIds.Destroy => ["摧毁"],
            BehaviorTemplateIds.Move => ["移动"],
            BehaviorTemplateIds.Recall => ["返回", "召回"],
            BehaviorTemplateIds.Recycle => ["回收"],
            BehaviorTemplateIds.Banish => ["放逐"],
            BehaviorTemplateIds.Stun => ["眩晕"],
            BehaviorTemplateIds.TempMight => ["{{S}}+", "{{S}}-", "战力"],
            BehaviorTemplateIds.Boon => ["增益"],
            BehaviorTemplateIds.GainExperience => ["经验"],
            BehaviorTemplateIds.Assemble => ["装配", "百炼"],
            BehaviorTemplateIds.Echo => ["回响"],
            BehaviorTemplateIds.Ambush => ["伏击"],
            _ => [templateId]
        };

        return TargetParser.SplitRulesText(text)
            .FirstOrDefault(segment => needles.Any(needle => segment.Contains(needle, StringComparison.Ordinal)))
            ?? string.Empty;
    }
}
