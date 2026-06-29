using System.Globalization;
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
    IReadOnlyList<StaticAuraSpec> StaticAuras,
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
            StaticAuraParser.Parse(text),
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
            null,
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
    private const int PowerfulUnitPowerThreshold = 5;

    public static IReadOnlyList<TriggerSpec> Parse(string text)
    {
        var triggers = new List<TriggerSpec>();
        var hasBattlefieldConquerRevealRecycleTrigger =
            TryParseBattlefieldConquerRevealRecycle(text, out var battlefieldConquerRevealRecycleTrigger);
        if (hasBattlefieldConquerRevealRecycleTrigger)
        {
            triggers.Add(battlefieldConquerRevealRecycleTrigger);
        }

        var hasBattlefieldConquerReadyEquipmentTrigger =
            TryParseBattlefieldConquerReadyEquipment(text, out var battlefieldConquerReadyEquipmentTrigger);
        if (hasBattlefieldConquerReadyEquipmentTrigger)
        {
            triggers.Add(battlefieldConquerReadyEquipmentTrigger);
        }

        var hasBattlefieldDefendRevealSpellTrigger =
            TryParseBattlefieldDefendRevealSpell(text, out var battlefieldDefendRevealSpellTrigger);
        if (hasBattlefieldDefendRevealSpellTrigger)
        {
            triggers.Add(battlefieldDefendRevealSpellTrigger);
        }

        var hasUnitLastBreathDrawOneTrigger =
            TryParseUnitLastBreathDrawOne(text, out var unitLastBreathDrawOneTrigger);
        if (hasUnitLastBreathDrawOneTrigger)
        {
            triggers.Add(unitLastBreathDrawOneTrigger);
        }

        var hasUnitLastBreathCallRuneTrigger =
            TryParseUnitLastBreathCallRuneOne(text, out var unitLastBreathCallRuneTrigger);
        if (hasUnitLastBreathCallRuneTrigger)
        {
            triggers.Add(unitLastBreathCallRuneTrigger);
        }

        var hasUnitLastBreathCreateDormantGoldTrigger =
            TryParseUnitLastBreathCreateDormantGold(text, out var unitLastBreathCreateDormantGoldTrigger);
        if (hasUnitLastBreathCreateDormantGoldTrigger)
        {
            triggers.Add(unitLastBreathCreateDormantGoldTrigger);
        }

        var hasUnitLastBreathDiscardDrawTrigger =
            TryParseUnitLastBreathDiscardDraw(text, out var unitLastBreathDiscardDrawTrigger);
        if (hasUnitLastBreathDiscardDrawTrigger)
        {
            triggers.Add(unitLastBreathDiscardDrawTrigger);
        }

        var hasUnitLastBreathPowerfulDrawTrigger =
            TryParseUnitLastBreathPowerfulDraw(text, out var unitLastBreathPowerfulDrawTrigger);
        if (hasUnitLastBreathPowerfulDrawTrigger)
        {
            triggers.Add(unitLastBreathPowerfulDrawTrigger);
        }

        var hasUnitLastBreathSourceBattlefieldAoeDamageTrigger =
            TryParseUnitLastBreathSourceBattlefieldAoeDamage(
                text,
                out var unitLastBreathSourceBattlefieldAoeDamageTrigger);
        if (hasUnitLastBreathSourceBattlefieldAoeDamageTrigger)
        {
            triggers.Add(unitLastBreathSourceBattlefieldAoeDamageTrigger);
        }

        var hasUnitLastBreathCreateBaseUnitTrigger =
            TryParseUnitLastBreathCreateBaseUnit(text, out var unitLastBreathCreateBaseUnitTrigger);
        if (hasUnitLastBreathCreateBaseUnitTrigger)
        {
            triggers.Add(unitLastBreathCreateBaseUnitTrigger);
        }

        var hasLegendHighCostSpellBanishCompletionTrigger =
            TryParseLegendHighCostSpellBanishCompletion(text, out var legendHighCostSpellBanishCompletionTrigger);
        if (hasLegendHighCostSpellBanishCompletionTrigger)
        {
            triggers.Add(legendHighCostSpellBanishCompletionTrigger);
        }

        triggers.AddRange(TargetParser.SplitRulesText(text)
            .Where(segment => segment.Contains("当", StringComparison.Ordinal)
                || segment.Contains("每当", StringComparison.Ordinal)
                || segment.Contains("打出我时", StringComparison.Ordinal)
                || segment.Contains("回合开始", StringComparison.Ordinal)
                || (segment.Contains("开始阶段开始时", StringComparison.Ordinal)
                    && segment.Contains("造成", StringComparison.Ordinal)
                    && segment.Contains("伤害", StringComparison.Ordinal))
                || (segment.Contains("开始阶段开始时", StringComparison.Ordinal)
                    && segment.Contains("摧毁", StringComparison.Ordinal)
                    && segment.Contains("抽", StringComparison.Ordinal))
                || segment.Contains("被摧毁", StringComparison.Ordinal)
                || segment.Contains("征服", StringComparison.Ordinal))
            .Where(segment => !hasBattlefieldConquerRevealRecycleTrigger
                || !segment.Contains("当你征服此处时，查看主牌堆顶部", StringComparison.Ordinal))
            .Where(segment => !hasBattlefieldConquerReadyEquipmentTrigger
                || !segment.Contains("当你征服此处时，你可以选择让", StringComparison.Ordinal)
                || !segment.Contains("友方装备变为活跃状态", StringComparison.Ordinal))
            .Where(segment => !hasBattlefieldDefendRevealSpellTrigger
                || !segment.Contains("当你防守此处时，展示你主牌堆顶部", StringComparison.Ordinal))
            .Where(segment => !hasLegendHighCostSpellBanishCompletionTrigger
                || !segment.Contains("当你打出一个法术时，如果消耗了不低于", StringComparison.Ordinal)
                || !segment.Contains("则你可以选择将该法术放逐", StringComparison.Ordinal))
            .Select(ToTriggerSpec)
            .ToArray());

        return triggers.ToArray();
    }

    private static bool TryParseLegendHighCostSpellBanishCompletion(string text, out TriggerSpec trigger)
    {
        trigger = default!;
        var match = Regex.Match(
            text,
            @"当你打出一个法术时，如果消耗了不低于\{\{(\d+)\}\}法力，则你可以选择将该法术放逐。?如果以此方法放逐了(.+)张法术牌，则将这些法术牌放入各自的废牌堆，召出(.+)枚符文，并抽一张牌",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return false;
        }

        trigger = new TriggerSpec(
            TriggerKinds.LegendHighCostSpellBanishCompletion,
            TriggerTimings.BattlefieldSpellPlayed,
            match.Value,
            "Legend high-cost spell banish completion trigger parsed for spell-play trigger routing; execution keeps the current representative auto-resolution while optional prompt breadth remains residual.",
            TargetScope: TriggerTargetScopes.SourceLegend,
            MinimumPaidMana: int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
            BanishCount: ParseChineseNumber(match.Groups[2].Value),
            RuneCallCount: ParseChineseNumber(match.Groups[3].Value),
            DrawCount: 1,
            Optional: true);
        return true;
    }

    private static bool TryParseUnitLastBreathDrawOne(string text, out TriggerSpec trigger)
    {
        trigger = default!;
        var match = Regex.Match(
            text,
            @"\{\{绝念\}\}\s*[—-]\s*抽一张牌",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return false;
        }

        trigger = new TriggerSpec(
            TriggerKinds.UnitLastBreathDrawOne,
            TriggerTimings.UnitDestroyed,
            match.Value,
            "Unit last-breath draw-one trigger parsed for destroyed-trigger routing; execution is available through shared unit-destroyed TriggerSpec resolution.",
            TargetScope: TriggerTargetScopes.SourceUnit,
            DrawCount: 1);
        return true;
    }

    private static bool TryParseUnitLastBreathCallRuneOne(string text, out TriggerSpec trigger)
    {
        trigger = default!;
        var match = Regex.Match(
            text,
            @"\{\{绝念>?\}\}\s*[—-]?\s*召出一枚休眠的符文",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return false;
        }

        trigger = new TriggerSpec(
            TriggerKinds.UnitLastBreathCallRuneOne,
            TriggerTimings.UnitDestroyed,
            match.Value,
            "Unit last-breath call-rune trigger parsed for destroyed-trigger routing; execution is available through shared unit-destroyed TriggerSpec resolution.",
            TargetScope: TriggerTargetScopes.SourceUnit,
            RuneCallCount: 1);
        return true;
    }

    private static bool TryParseUnitLastBreathCreateDormantGold(string text, out TriggerSpec trigger)
    {
        trigger = default!;
        var match = Regex.Match(
            text,
            @"\{\{绝念>?\}\}\s*[—-]?\s*打出([0-9一两二三四五六七八九十]+)?个休眠的“金币”装备指示物",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return false;
        }

        var rawCount = match.Groups[1].Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value)
            ? match.Groups[1].Value
            : "一";
        trigger = new TriggerSpec(
            TriggerKinds.UnitLastBreathCreateDormantGold,
            TriggerTimings.UnitDestroyed,
            match.Value,
            "Unit last-breath create-dormant-Gold trigger parsed for destroyed-trigger routing; execution is available through shared unit-destroyed TriggerSpec resolution.",
            TargetScope: TriggerTargetScopes.SourceUnit,
            CreatedTokenCount: ParseChineseNumber(rawCount),
            CreatedTokenName: "金币",
            CreatedTokenDestination: TriggerTokenDestinations.OwnerBase,
            CreatedTokenExhausted: true,
            CreatedTokenKeywords: ["反应"]);
        return true;
    }

    private static bool TryParseUnitLastBreathDiscardDraw(string text, out TriggerSpec trigger)
    {
        trigger = default!;
        var match = Regex.Match(
            text,
            @"\{\{绝念>?\}\}\s*[—-]?\s*弃置([0-9一两二三四五六七八九十]+)张手牌，然后抽([0-9一两二三四五六七八九十]+)张牌",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return false;
        }

        trigger = new TriggerSpec(
            TriggerKinds.UnitLastBreathDiscardDraw,
            TriggerTimings.UnitDestroyed,
            match.Value,
            "Unit last-breath discard-draw trigger parsed for destroyed-trigger routing; execution is available through shared unit-destroyed TriggerSpec resolution.",
            TargetScope: TriggerTargetScopes.SourceUnit,
            DiscardCount: ParseChineseNumber(match.Groups[1].Value),
            DrawCount: ParseChineseNumber(match.Groups[2].Value));
        return true;
    }

    private static bool TryParseUnitLastBreathPowerfulDraw(string text, out TriggerSpec trigger)
    {
        trigger = default!;
        var match = Regex.Match(
            text,
            @"\{\{绝念>?\}\}\s*[—-]?\s*如果我为\{\{强力\}\}单位，则抽([0-9一两二三四五六七八九十]+)张牌",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return false;
        }

        trigger = new TriggerSpec(
            TriggerKinds.UnitLastBreathPowerfulDraw,
            TriggerTimings.UnitDestroyed,
            match.Value,
            "Unit last-breath powerful draw trigger parsed for destroyed-trigger routing; execution is available through shared unit-destroyed TriggerSpec resolution.",
            TargetScope: TriggerTargetScopes.SourceUnit,
            DrawCount: ParseChineseNumber(match.Groups[1].Value),
            RequiredPowerThreshold: PowerfulUnitPowerThreshold);
        return true;
    }

    private static bool TryParseUnitLastBreathSourceBattlefieldAoeDamage(string text, out TriggerSpec trigger)
    {
        trigger = default!;
        var match = Regex.Match(
            text,
            @"\{\{绝念>?\}\}\s*[—-]?\s*对我所处战场上的所有单位各造成([0-9一两二三四五六七八九十]+)点伤害",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return false;
        }

        trigger = new TriggerSpec(
            TriggerKinds.UnitLastBreathDamageSourceBattlefieldUnits,
            TriggerTimings.UnitDestroyed,
            match.Value,
            "Unit last-breath source-battlefield AoE damage trigger parsed for destroyed-trigger routing; execution is available through shared unit-destroyed TriggerSpec resolution.",
            TargetScope: TriggerTargetScopes.SourceBattlefieldUnits,
            DamageAmount: ParseChineseNumber(match.Groups[1].Value));
        return true;
    }

    private static bool TryParseUnitLastBreathCreateBaseUnit(string text, out TriggerSpec trigger)
    {
        trigger = default!;
        var match = Regex.Match(
            text,
            @"\{\{绝念>?\}\}\s*[—-]?\s*打出([0-9一两二三四五六七八九十]+)名([0-9一两二三四五六七八九十]+)\{\{S\}\}的?“([^”]+)”到你的基地(?:，它拥有\{\{([^}]+)\}\})?",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return false;
        }

        var tokenCount = ParseChineseNumber(match.Groups[1].Value);
        var createdTokenPower = ParseChineseNumber(match.Groups[2].Value);
        var tokenName = match.Groups[3].Value;
        var tokenKeywords = match.Groups[4].Success
            && !string.IsNullOrWhiteSpace(match.Groups[4].Value)
                ? new[] { match.Groups[4].Value }
                : Array.Empty<string>();
        var kind = UnitLastBreathCreateBaseUnitKind(tokenName, tokenCount, createdTokenPower, tokenKeywords);
        if (string.IsNullOrWhiteSpace(kind))
        {
            return false;
        }

        trigger = new TriggerSpec(
            kind,
            TriggerTimings.UnitDestroyed,
            match.Value,
            "Unit last-breath create-base-unit trigger parsed for destroyed-trigger routing; execution is available through shared unit-destroyed TriggerSpec resolution.",
            TargetScope: TriggerTargetScopes.SourceUnit,
            CreatedTokenCount: tokenCount,
            CreatedTokenName: tokenName,
            CreatedTokenPower: createdTokenPower,
            CreatedTokenDestination: TriggerTokenDestinations.OwnerBase,
            CreatedTokenKeywords: tokenKeywords);
        return true;
    }

    private static bool TryParseBattlefieldConquerRevealRecycle(string text, out TriggerSpec trigger)
    {
        trigger = default!;
        var match = Regex.Match(
            text,
            @"当你征服此处时，查看主牌堆顶部的([0-9一两二三四五六七八九十]+)张牌。你可以选择从这[0-9一两二三四五六七八九十]+张牌中回收任意数量的卡牌，并将其余的卡牌按任意顺序放回原处",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return false;
        }

        var revealCount = ParseChineseNumber(match.Groups[1].Value);
        trigger = new TriggerSpec(
            TriggerKinds.BattlefieldConquerRevealRecycle,
            TriggerTimings.BattlefieldConquered,
            match.Value,
            "Battlefield conquered reveal/recycle trigger parsed for B4 routing; execution is available as a deterministic representative path when engine support reads BehaviorSpec.Triggers.",
            RevealCount: revealCount,
            RevealSourceZone: TriggerZones.MainDeck,
            RecycleCount: revealCount,
            RecycleDestinationZone: TriggerZones.MainDeck);
        return true;
    }

    private static bool TryParseBattlefieldConquerReadyEquipment(string text, out TriggerSpec trigger)
    {
        trigger = default!;
        var match = Regex.Match(
            text,
            @"当你征服此处时，你可以选择让([0-9一两二三四五六七八九十]+)件友方装备变为活跃状态。?如果它是一件武装，则你可以选择将其卸除",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return false;
        }

        trigger = new TriggerSpec(
            TriggerKinds.BattlefieldConquerReadyEquipment,
            TriggerTimings.BattlefieldConquered,
            match.Value,
            "Battlefield conquered ready-equipment trigger parsed for B4 routing; execution is available as an auto-resolution representative path when engine support reads BehaviorSpec.Triggers.",
            TargetScope: TriggerTargetScopes.FriendlyEquipment,
            EquipmentReadyCount: ParseChineseNumber(match.Groups[1].Value),
            DetachesArmament: true);
        return true;
    }

    private static bool TryParseBattlefieldDefendRevealSpell(string text, out TriggerSpec trigger)
    {
        trigger = default!;
        var match = Regex.Match(
            text,
            @"当你防守此处时，展示你主牌堆顶部的([0-9一两二三四五六七八九十]+)张牌。?如果是一张法术牌，则将其放入你的手牌，否则将其回收",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return false;
        }

        trigger = new TriggerSpec(
            TriggerKinds.BattlefieldDefendRevealTopDrawSpellOrRecycle,
            TriggerTimings.BattlefieldDefended,
            match.Value,
            "Battlefield defended reveal-top spell-or-recycle trigger parsed for B4 routing; execution is available as an auto-resolution representative path when engine support reads BehaviorSpec.Triggers.",
            RevealCount: ParseChineseNumber(match.Groups[1].Value),
            RevealSourceZone: TriggerZones.MainDeck,
            RevealMatchCardFilter: TriggerCardFilters.TagPrefix + "CARD_TYPE:SPELL",
            RevealMatchDestinationZone: TriggerZones.Hand,
            RevealMissDestinationZone: TriggerZones.MainDeck);
        return true;
    }

    private static TriggerSpec ToTriggerSpec(string segment)
    {
        var unitBattlefieldHeldDrawMatch = Regex.Match(
            segment,
            @"当我据守一处战场时，抽([0-9一两二三四五六七八九十]+)张牌。?$",
            RegexOptions.CultureInvariant);
        if (unitBattlefieldHeldDrawMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.UnitBattlefieldHeldDraw,
                TriggerTimings.BattlefieldHeld,
                segment,
                "Unit battlefield-held draw trigger parsed for B4 routing; execution is available through shared unit battlefield-held TriggerSpec resolution.",
                TargetScope: TriggerTargetScopes.SourceUnit,
                DrawCount: ParseChineseNumber(unitBattlefieldHeldDrawMatch.Groups[1].Value));
        }

        var unitBoonGrantedReadySelfMatch = Regex.Match(
            segment,
            @"当你给予我增益时，让我变为活跃状态。?$",
            RegexOptions.CultureInvariant);
        if (unitBoonGrantedReadySelfMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.UnitBoonGrantedReadySelf,
                TriggerTimings.UnitBoonGranted,
                segment,
                "Unit boon-granted ready-self trigger parsed for B5 routing; execution is available through shared unit boon-granted TriggerSpec resolution.",
                TargetScope: TriggerTargetScopes.SourceUnit,
                ReadiesSource: true);
        }

        var unitConquestGrantSelfBoonMatch = Regex.Match(
            segment,
            @"当我被打出时、或当我征服一处战场时，给予我增益。?$",
            RegexOptions.CultureInvariant);
        if (unitConquestGrantSelfBoonMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.UnitConquestGrantSelfBoon,
                TriggerTimings.UnitConquest,
                segment,
                "Unit conquest grant-self-boon trigger parsed for B3 routing; execution is available through shared unit-conquest TriggerSpec resolution.",
                TargetScope: TriggerTargetScopes.SourceUnit,
                BoonCount: 1);
        }

        var unitConquestReadySelfOnceMatch = Regex.Match(
            segment,
            @"每回合首次，当我征服一处战场时，让我变为活跃状态。?$",
            RegexOptions.CultureInvariant);
        if (unitConquestReadySelfOnceMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.UnitConquestReadySelfOncePerTurn,
                TriggerTimings.UnitConquest,
                segment,
                "Unit conquest ready-self once-per-turn trigger parsed for B3 routing; execution is available through shared unit-conquest TriggerSpec resolution.",
                TargetScope: TriggerTargetScopes.SourceUnit,
                OncePerTurn: true);
        }

        var unitConquestGrantFriendlyBoonMatch = Regex.Match(
            segment,
            @"当我征服一处战场时，给予一名友方单位(?:\{\{增益\}\}|增益)。?$",
            RegexOptions.CultureInvariant);
        if (unitConquestGrantFriendlyBoonMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.UnitConquestGrantFriendlyBoon,
                TriggerTimings.UnitConquest,
                segment,
                "Unit conquest grant-friendly-boon trigger parsed for B3 routing; execution is available through shared unit-conquest TriggerSpec resolution.",
                TargetScope: TriggerTargetScopes.ControlledUnitOnField,
                BoonCount: 1);
        }

        var unitConquestAdditionalActivationMatch = Regex.Match(
            segment,
            @"你征服此处时的征服效果额外触发([0-9一两二三四五六七八九十]+)次。?$",
            RegexOptions.CultureInvariant);
        if (unitConquestAdditionalActivationMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.UnitConquestAdditionalActivation,
                TriggerTimings.BattlefieldConquered,
                segment,
                "Unit conquest additional-activation trigger parsed for B3 routing; execution is available through shared unit-conquest TriggerSpec resolution when its controller conquers this battlefield.",
                TargetScope: TriggerTargetScopes.ControlledUnitsAtThisBattlefield,
                AdditionalTriggerCount: ParseChineseNumber(unitConquestAdditionalActivationMatch.Groups[1].Value));
        }

        var unitConquestFriendlyPowerUntilEndMatch = Regex.Match(
            segment,
            @"当我征服一处战场时，让一名友方单位本回合内\{\{S\}\}\+([0-9一两二三四五六七八九十]+)。?$",
            RegexOptions.CultureInvariant);
        if (unitConquestFriendlyPowerUntilEndMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.UnitConquestFriendlyPowerUntilEndOfTurn,
                TriggerTimings.UnitConquest,
                segment,
                "Unit conquest friendly-power trigger parsed for B3 routing; execution is available through shared unit-conquest TriggerSpec resolution.",
                TargetScope: TriggerTargetScopes.ControlledUnitOnField,
                PowerDelta: ParseChineseNumber(unitConquestFriendlyPowerUntilEndMatch.Groups[1].Value),
                Duration: TriggerDurations.UntilEndOfTurn);
        }

        var unitConquestDestroyEquipmentGrantSelfBoonMatch = Regex.Match(
            segment,
            @"当我征服一处战场时，你可以选择摧毁一件装备，以此给予我增益。?$",
            RegexOptions.CultureInvariant);
        if (unitConquestDestroyEquipmentGrantSelfBoonMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.UnitConquestDestroyEquipmentGrantSelfBoon,
                TriggerTimings.UnitConquest,
                segment,
                "Unit conquest destroy-equipment grant-self-boon trigger parsed for B3 routing; execution is available through shared unit-conquest TriggerSpec resolution.",
                TargetScope: TriggerTargetScopes.EquipmentOnField,
                DestroyCount: 1,
                BoonCount: 1,
                Optional: true);
        }

        var unitConquestPayReturnSelfToHandMatch = Regex.Match(
            segment,
            @"每?当我征服一处战场时，你可以选择支付\{\{([0-9一两二三四五六七八九十]+)\}\}来让我返回所属的手牌。?$",
            RegexOptions.CultureInvariant);
        if (unitConquestPayReturnSelfToHandMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.UnitConquestPayReturnSelfToHand,
                TriggerTimings.UnitConquest,
                segment,
                "Unit conquest pay-return-self trigger parsed for B3 trigger-payment routing; execution is available through shared unit-conquest TriggerSpec resolution.",
                TargetScope: TriggerTargetScopes.SourceUnit,
                ManaCost: ParseChineseNumber(unitConquestPayReturnSelfToHandMatch.Groups[1].Value),
                ReturnCount: 1,
                ReturnOriginZone: TriggerZones.Battlefield,
                ReturnDestinationZone: TriggerZones.Hand,
                Optional: true);
        }

        var unitFriendlyDestroyedGainExperienceMatch = Regex.Match(
            segment,
            @"当另一名友方单位被摧毁时，获得([0-9一两二三四五六七八九十]+)经验。?$",
            RegexOptions.CultureInvariant);
        if (unitFriendlyDestroyedGainExperienceMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.UnitFriendlyDestroyedGainExperience,
                TriggerTimings.UnitDestroyed,
                segment,
                "Unit friendly-destroyed gain-experience trigger parsed for destroyed-trigger routing; execution is available through shared unit-destroyed TriggerSpec resolution.",
                TargetScope: TriggerTargetScopes.OtherFriendlyDestroyedUnit,
                ExperienceCount: ParseChineseNumber(unitFriendlyDestroyedGainExperienceMatch.Groups[1].Value));
        }

        var unitFriendlyDestroyedPowerUntilEndMatch = Regex.Match(
            segment,
            @"当另一名友方单位被摧毁时，让我本回合内\{\{S\}\}\+([0-9一两二三四五六七八九十]+)。?$",
            RegexOptions.CultureInvariant);
        if (unitFriendlyDestroyedPowerUntilEndMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.UnitFriendlyDestroyedPowerUntilEndOfTurn,
                TriggerTimings.UnitDestroyed,
                segment,
                "Unit friendly-destroyed power trigger parsed for destroyed-trigger routing; execution is available through shared unit-destroyed TriggerSpec resolution.",
                TargetScope: TriggerTargetScopes.OtherFriendlyDestroyedUnit,
                PowerDelta: ParseChineseNumber(unitFriendlyDestroyedPowerUntilEndMatch.Groups[1].Value),
                Duration: TriggerDurations.UntilEndOfTurn);
        }

        var unitFirstFriendlyDestroyedDrawMatch = Regex.Match(
            segment,
            @"每回合首次：当你的友方单位被摧毁时，抽一张牌。?$",
            RegexOptions.CultureInvariant);
        if (unitFirstFriendlyDestroyedDrawMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.UnitFirstFriendlyDestroyedDrawOne,
                TriggerTimings.UnitDestroyed,
                segment,
                "Unit first-friendly-destroyed draw trigger parsed for destroyed-trigger routing; execution is available through shared unit-destroyed TriggerSpec resolution.",
                TargetScope: TriggerTargetScopes.OtherFriendlyDestroyedUnit,
                DrawCount: 1,
                OncePerTurn: true);
        }

        var unitDestroyedNonMinionCreateMinionMatch = Regex.Match(
            segment,
            @"如果我在场上，则每当你的另一名非“随从”单位被摧毁时，打出一名([0-9一两二三四五六七八九十]+)\{\{S\}\}的“随从”到你的基地。?$",
            RegexOptions.CultureInvariant);
        if (unitDestroyedNonMinionCreateMinionMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.UnitDestroyedNonMinionCreateMinion,
                TriggerTimings.UnitDestroyed,
                segment,
                "Unit destroyed non-minion create-minion trigger parsed for destroyed-trigger routing; execution is available through shared unit-destroyed TriggerSpec resolution.",
                TargetScope: TriggerTargetScopes.OtherFriendlyDestroyedUnit,
                ExcludesTokens: true,
                CreatedTokenCount: 1,
                CreatedTokenName: "随从",
                CreatedTokenPower: ParseChineseNumber(unitDestroyedNonMinionCreateMinionMatch.Groups[1].Value),
                CreatedTokenDestination: TriggerTokenDestinations.OwnerBase);
        }

        var unitLastBreathDrawIfAloneMatch = Regex.Match(
            segment,
            @"(?:\{\{绝念\}\}\s*[—-]\s*)?当我被摧毁时，如果此处没有其他友方单位，则抽一张牌。?$",
            RegexOptions.CultureInvariant);
        if (unitLastBreathDrawIfAloneMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.UnitLastBreathDrawIfAlone,
                TriggerTimings.UnitDestroyed,
                segment,
                "Unit last-breath draw-if-alone trigger parsed for destroyed-trigger routing; execution is available through shared unit-destroyed TriggerSpec resolution.",
                TargetScope: TriggerTargetScopes.SourceUnit,
                DrawCount: 1,
                RequiresNoOtherFriendlyUnitAtSamePosition: true);
        }

        var unitLastBreathDrawIfNotAloneMatch = Regex.Match(
            segment,
            @"(?:\{\{绝念>?\}\}\s*[—-]?\s*)?如果我被摧毁时未处于落单状态，则抽一张牌。?$",
            RegexOptions.CultureInvariant);
        if (unitLastBreathDrawIfNotAloneMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.UnitLastBreathDrawIfNotAlone,
                TriggerTimings.UnitDestroyed,
                segment,
                "Unit last-breath draw-if-not-alone trigger parsed for destroyed-trigger routing; execution is available through shared unit-destroyed TriggerSpec resolution.",
                TargetScope: TriggerTargetScopes.SourceUnit,
                DrawCount: 1,
                RequiresOtherFriendlyUnitAtSamePosition: true);
        }

        var unitLastBreathCreateBaseUnitMatch = Regex.Match(
            segment,
            @"(?:\{\{绝念>?\}\}\s*[—-]?\s*)?打出([0-9一两二三四五六七八九十]+)名([0-9一两二三四五六七八九十]+)\{\{S\}\}的?“([^”]+)”到你的基地(?:，它拥有\{\{([^}]+)\}\})?。?$",
            RegexOptions.CultureInvariant);
        if (unitLastBreathCreateBaseUnitMatch.Success)
        {
            var tokenCount = ParseChineseNumber(unitLastBreathCreateBaseUnitMatch.Groups[1].Value);
            var createdTokenPower = ParseChineseNumber(unitLastBreathCreateBaseUnitMatch.Groups[2].Value);
            var tokenName = unitLastBreathCreateBaseUnitMatch.Groups[3].Value;
            var tokenKeywords = unitLastBreathCreateBaseUnitMatch.Groups[4].Success
                && !string.IsNullOrWhiteSpace(unitLastBreathCreateBaseUnitMatch.Groups[4].Value)
                    ? new[] { unitLastBreathCreateBaseUnitMatch.Groups[4].Value }
                    : Array.Empty<string>();
            var kind = UnitLastBreathCreateBaseUnitKind(tokenName, tokenCount, createdTokenPower, tokenKeywords);
            if (!string.IsNullOrWhiteSpace(kind))
            {
                return new TriggerSpec(
                    kind,
                    TriggerTimings.UnitDestroyed,
                    segment,
                    "Unit last-breath create-base-unit trigger parsed for destroyed-trigger routing; execution is available through shared unit-destroyed TriggerSpec resolution.",
                    TargetScope: TriggerTargetScopes.SourceUnit,
                    CreatedTokenCount: tokenCount,
                    CreatedTokenName: tokenName,
                    CreatedTokenPower: createdTokenPower,
                    CreatedTokenDestination: TriggerTokenDestinations.OwnerBase,
                    CreatedTokenKeywords: tokenKeywords);
            }
        }

        var unitConquestOverkillCreateDormantGoldMatch = Regex.Match(
            segment,
            @"当我征服一处战场时，如果你给敌方单位分配了不低于([0-9一两二三四五六七八九十]+)点的过量伤害，则打出([0-9一两二三四五六七八九十]+)个休眠的“金币”装备指示物。?$",
            RegexOptions.CultureInvariant);
        if (unitConquestOverkillCreateDormantGoldMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.UnitConquestOverkillCreateDormantGold,
                TriggerTimings.UnitConquest,
                segment,
                "Unit conquest overkill create-dormant-Gold trigger parsed for B3 routing; execution is available through shared unit-conquest TriggerSpec resolution.",
                TargetScope: TriggerTargetScopes.SourceUnit,
                RequiredOverkillDamage: ParseChineseNumber(unitConquestOverkillCreateDormantGoldMatch.Groups[1].Value),
                CreatedTokenCount: ParseChineseNumber(unitConquestOverkillCreateDormantGoldMatch.Groups[2].Value),
                CreatedTokenName: "金币",
                CreatedTokenDestination: TriggerTokenDestinations.OwnerBase,
                CreatedTokenExhausted: true,
                CreatedTokenKeywords: ["反应"]);
        }

        var unitConquestAttackOverkillGainScoreMatch = Regex.Match(
            segment,
            @"当我通过进攻征服一处战场时，如果你给敌方单位造成过不低于([0-9一两二三四五六七八九十]+)点的过量伤害，则你获得的分数\+([0-9一两二三四五六七八九十]+)。?$",
            RegexOptions.CultureInvariant);
        if (unitConquestAttackOverkillGainScoreMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.UnitConquestAttackOverkillGainScore,
                TriggerTimings.UnitConquest,
                segment,
                "Unit conquest attack-overkill gain-score trigger parsed for B3 routing; execution is available through shared unit-conquest TriggerSpec resolution.",
                TargetScope: TriggerTargetScopes.SourceUnit,
                RequiredOverkillDamage: ParseChineseNumber(unitConquestAttackOverkillGainScoreMatch.Groups[1].Value),
                ScoreAmount: ParseChineseNumber(unitConquestAttackOverkillGainScoreMatch.Groups[2].Value));
        }

        var unitConquestCreateDormantGoldMatch = Regex.Match(
            segment,
            @"当我征服一处战场时，打出([0-9一两二三四五六七八九十]+)?个休眠的“金币”装备指示物。?$",
            RegexOptions.CultureInvariant);
        if (unitConquestCreateDormantGoldMatch.Success)
        {
            var rawCount = unitConquestCreateDormantGoldMatch.Groups[1].Success
                && !string.IsNullOrWhiteSpace(unitConquestCreateDormantGoldMatch.Groups[1].Value)
                    ? unitConquestCreateDormantGoldMatch.Groups[1].Value
                    : "一";
            return new TriggerSpec(
                TriggerKinds.UnitConquestCreateDormantGold,
                TriggerTimings.UnitConquest,
                segment,
                "Unit conquest create-dormant-Gold trigger parsed for B3 routing; execution is available through shared unit-conquest TriggerSpec resolution.",
                TargetScope: TriggerTargetScopes.SourceUnit,
                CreatedTokenCount: ParseChineseNumber(rawCount),
                CreatedTokenName: "金币",
                CreatedTokenDestination: TriggerTokenDestinations.OwnerBase,
                CreatedTokenExhausted: true,
                CreatedTokenKeywords: ["反应"]);
        }

        var unitMovedCreateDormantGoldMatch = Regex.Match(
            segment,
            @"每当(?:我|其)移动时，打出([0-9一两二三四五六七八九十]+)?个休眠的“金币”装备指示物。?$",
            RegexOptions.CultureInvariant);
        if (unitMovedCreateDormantGoldMatch.Success)
        {
            var rawCount = unitMovedCreateDormantGoldMatch.Groups[1].Success
                && !string.IsNullOrWhiteSpace(unitMovedCreateDormantGoldMatch.Groups[1].Value)
                    ? unitMovedCreateDormantGoldMatch.Groups[1].Value
                    : "一";
            return new TriggerSpec(
                TriggerKinds.UnitMovedCreateDormantGold,
                TriggerTimings.UnitMoved,
                segment,
                "Unit moved create-dormant-Gold trigger parsed for movement-trigger routing; execution is available through shared unit-moved TriggerSpec resolution.",
                TargetScope: TriggerTargetScopes.SourceUnit,
                CreatedTokenCount: ParseChineseNumber(rawCount),
                CreatedTokenName: "金币",
                CreatedTokenDestination: TriggerTokenDestinations.OwnerBase,
                CreatedTokenExhausted: true,
                CreatedTokenKeywords: ["反应"]);
        }

        var handCardsDiscardedReadySourcePowerMatch = Regex.Match(
            segment,
            @"每当你弃置任意数量的手牌时，让我变为活跃状态，且本回合内\{\{S\}\}\+([0-9一两二三四五六七八九十]+)。?$",
            RegexOptions.CultureInvariant);
        if (handCardsDiscardedReadySourcePowerMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.HandCardsDiscardedReadySourcePower,
                TriggerTimings.HandCardsDiscarded,
                segment,
                "Hand-discard ready-source power trigger parsed for discard-trigger routing; execution is available through shared hand-discard TriggerSpec resolution.",
                TargetScope: TriggerTargetScopes.SourceUnit,
                PowerDelta: ParseChineseNumber(handCardsDiscardedReadySourcePowerMatch.Groups[1].Value),
                Duration: TriggerDurations.UntilEndOfTurn,
                ReadiesSource: true);
        }

        var unitArmamentAttachedPayDrawMatch = Regex.Match(
            segment,
            @"当你为我贴附武装时，可以选择支付\{\{([0-9一两二三四五六七八九十]+)\}\}，以此抽([0-9一两二三四五六七八九十]+)张牌。?$",
            RegexOptions.CultureInvariant);
        if (unitArmamentAttachedPayDrawMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.UnitArmamentAttachedPayDraw,
                TriggerTimings.UnitArmamentAttached,
                segment,
                "Unit armament-attached pay-draw trigger parsed for trigger-payment routing; execution is available through shared unit trigger-payment TriggerSpec resolution.",
                TargetScope: TriggerTargetScopes.FriendlyEquipment,
                ManaCost: ParseChineseNumber(unitArmamentAttachedPayDrawMatch.Groups[1].Value),
                DrawCount: ParseChineseNumber(unitArmamentAttachedPayDrawMatch.Groups[2].Value),
                Optional: true);
        }

        var unitControlledUnitPowerfulPayReadyMatch = Regex.Match(
            segment,
            @"当你控制的一名单位变为\{\{强力\}\}时，你可以选择支付\{\{([^}]+)\}\}，以此让其变为活跃状态(?:。?（战力达到([0-9一两二三四五六七八九十]+)或以上时，即为强力单位。）)?。?$",
            RegexOptions.CultureInvariant);
        if (unitControlledUnitPowerfulPayReadyMatch.Success
            && TryParsePowerTrait(unitControlledUnitPowerfulPayReadyMatch.Groups[1].Value, out var powerTrait))
        {
            var requiredPowerThreshold = unitControlledUnitPowerfulPayReadyMatch.Groups[2].Success
                ? ParseChineseNumber(unitControlledUnitPowerfulPayReadyMatch.Groups[2].Value)
                : PowerfulUnitPowerThreshold;
            return new TriggerSpec(
                TriggerKinds.UnitControlledUnitPowerfulPayPowerReady,
                TriggerTimings.ControlledUnitBecamePowerful,
                segment,
                "Unit controlled-unit becomes-powerful pay-power ready trigger parsed for trigger-payment routing; execution is available through shared unit trigger-payment TriggerSpec resolution.",
                TargetScope: TriggerTargetScopes.ControlledUnitOnField,
                PowerCost: 1,
                PowerCostTrait: powerTrait,
                RequiredPowerThreshold: requiredPowerThreshold,
                UnitReadyCount: 1,
                Optional: true);
        }

        var unitConquestDrawOrCallRuneMatch = Regex.Match(
            segment,
            @"当我征服一处战场时，抽([0-9一两二三四五六七八九十]+)张牌或召出([0-9一两二三四五六七八九十]+)枚休眠的符文。?$",
            RegexOptions.CultureInvariant);
        if (unitConquestDrawOrCallRuneMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.UnitConquestDrawOneOrCallRune,
                TriggerTimings.UnitConquest,
                segment,
                "Unit conquest draw-or-call-rune trigger parsed for B3 routing; execution is available through shared unit-conquest TriggerSpec resolution.",
                TargetScope: TriggerTargetScopes.SourceUnit,
                DrawCount: ParseChineseNumber(unitConquestDrawOrCallRuneMatch.Groups[1].Value),
                RuneCallCount: ParseChineseNumber(unitConquestDrawOrCallRuneMatch.Groups[2].Value));
        }

        var unitConquestDrawMatch = Regex.Match(
            segment,
            @"当我征服一处战场时，抽([0-9一两二三四五六七八九十]+)张牌。?$",
            RegexOptions.CultureInvariant);
        if (unitConquestDrawMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.UnitConquestDrawOne,
                TriggerTimings.UnitConquest,
                segment,
                "Unit conquest draw-one trigger parsed for B3 routing; execution is available through shared unit-conquest TriggerSpec resolution.",
                TargetScope: TriggerTargetScopes.SourceUnit,
                DrawCount: ParseChineseNumber(unitConquestDrawMatch.Groups[1].Value));
        }

        var battlefieldConquerMillMatch = Regex.Match(
            segment,
            @"当你征服此处时，将你主牌堆顶部的([0-9一两二三四五六七八九十]+)张牌放入废牌堆",
            RegexOptions.CultureInvariant);
        if (battlefieldConquerMillMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.BattlefieldConquerMill,
                TriggerTimings.BattlefieldConquered,
                segment,
                "Battlefield conquered mill trigger parsed for B4 routing; execution is available when engine support reads BehaviorSpec.Triggers.",
                MillCount: ParseChineseNumber(battlefieldConquerMillMatch.Groups[1].Value),
                MillSourceZone: TriggerZones.MainDeck,
                MillDestinationZone: TriggerZones.Graveyard);
        }

        var battlefieldConquerRecycleRuneMatch = Regex.Match(
            segment,
            @"当你征服此处时，回收一枚你的符文",
            RegexOptions.CultureInvariant);
        if (battlefieldConquerRecycleRuneMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.BattlefieldConquerRecycleRune,
                TriggerTimings.BattlefieldConquered,
                segment,
                "Battlefield conquered recycle-rune trigger parsed for B4 routing; execution is available when engine support reads BehaviorSpec.Triggers.",
                TargetScope: TriggerTargetScopes.OwnedRuneInBase,
                RecycleCount: 1,
                RecycleSourceZone: TriggerZones.Base,
                RecycleDestinationZone: TriggerZones.MainDeck);
        }

        var battlefieldConquerConsumeBoonDrawMatch = Regex.Match(
            segment,
            @"当你征服此处时，你可以选择消耗([0-9一两二三四五六七八九十]+)个增益，以此抽([0-9一两二三四五六七八九十]+)张牌",
            RegexOptions.CultureInvariant);
        if (battlefieldConquerConsumeBoonDrawMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.BattlefieldConquerConsumeBoonDraw,
                TriggerTimings.BattlefieldConquered,
                segment,
                "Battlefield conquered consume-boon draw trigger parsed for B4 routing; execution is available as an auto-resolution representative path when engine support reads BehaviorSpec.Triggers.",
                TargetScope: TriggerTargetScopes.ControlledBoonUnitOnField,
                DrawCount: ParseChineseNumber(battlefieldConquerConsumeBoonDrawMatch.Groups[2].Value),
                ConsumedBoonCount: ParseChineseNumber(battlefieldConquerConsumeBoonDrawMatch.Groups[1].Value));
        }

        var battlefieldConquerDiscardDrawMatch = Regex.Match(
            segment,
            @"当你征服此处时，弃置([0-9一两二三四五六七八九十]+)张手牌，然后抽([0-9一两二三四五六七八九十]+)张牌",
            RegexOptions.CultureInvariant);
        if (battlefieldConquerDiscardDrawMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.BattlefieldConquerDiscardDraw,
                TriggerTimings.BattlefieldConquered,
                segment,
                "Battlefield conquered discard-draw trigger parsed for B4 routing; execution is available as an auto-resolution representative path when engine support reads BehaviorSpec.Triggers.",
                TargetScope: TriggerTargetScopes.ControlledHandCard,
                DrawCount: ParseChineseNumber(battlefieldConquerDiscardDrawMatch.Groups[2].Value),
                DiscardCount: ParseChineseNumber(battlefieldConquerDiscardDrawMatch.Groups[1].Value),
                DiscardSourceZone: TriggerZones.Hand,
                DiscardDestinationZone: TriggerZones.Graveyard);
        }

        var battlefieldConquerDrawForOtherBattlefieldsMatch = Regex.Match(
            segment,
            @"当你征服此处时，你和盟友每控制一处其他战场，你便抽([0-9一两二三四五六七八九十]+)张牌",
            RegexOptions.CultureInvariant);
        if (battlefieldConquerDrawForOtherBattlefieldsMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.BattlefieldConquerDrawForOtherBattlefields,
                TriggerTimings.BattlefieldConquered,
                segment,
                "Battlefield conquered draw-for-other-battlefields trigger parsed for B4 routing; execution is available when engine support reads BehaviorSpec.Triggers.",
                TargetScope: TriggerTargetScopes.OtherControlledBattlefields,
                DrawCountPerParticipant: ParseChineseNumber(battlefieldConquerDrawForOtherBattlefieldsMatch.Groups[1].Value));
        }

        var battlefieldConquerPowerfulPayDrawMatch = Regex.Match(
            segment,
            @"当你征服此处时，如果此战场上留存至少一名\{\{强力\}\}单位，则你可以选择支付\{\{([0-9一两二三四五六七八九十]+)\}\}来抽([0-9一两二三四五六七八九十]+)张牌",
            RegexOptions.CultureInvariant);
        if (battlefieldConquerPowerfulPayDrawMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.BattlefieldConquerPowerfulPayDraw,
                TriggerTimings.BattlefieldConquered,
                segment,
                "Battlefield conquered powerful-unit pay-draw trigger parsed for B4 routing; execution is available as an auto-resolution representative path when engine support reads BehaviorSpec.Triggers.",
                TargetScope: TriggerTargetScopes.SurvivingPowerfulUnitAtThisBattlefield,
                ManaCost: ParseChineseNumber(battlefieldConquerPowerfulPayDrawMatch.Groups[1].Value),
                DrawCount: ParseChineseNumber(battlefieldConquerPowerfulPayDrawMatch.Groups[2].Value),
                RequiredPowerThreshold: PowerfulUnitPowerThreshold);
        }

        var battlefieldConquerReadyRunesAtEndMatch = Regex.Match(
            segment,
            @"当你征服此处时，选择([0-9一两二三四五六七八九十]+)枚符文，并在本回合结束时，让它们变为活跃状态",
            RegexOptions.CultureInvariant);
        if (battlefieldConquerReadyRunesAtEndMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.BattlefieldConquerReadyRunesAtEnd,
                TriggerTimings.BattlefieldConquered,
                segment,
                "Battlefield conquered ready-runes-at-end trigger parsed for B4 routing; execution is available when engine support reads BehaviorSpec.Triggers.",
                TargetScope: TriggerTargetScopes.OwnedRuneInBase,
                RuneReadyCount: ParseChineseNumber(battlefieldConquerReadyRunesAtEndMatch.Groups[1].Value),
                ReadyTiming: TriggerReadyTimings.EndOfTurn);
        }

        var battlefieldConquerReadyEquipmentMatch = Regex.Match(
            segment,
            @"当你征服此处时，你可以选择让([0-9一两二三四五六七八九十]+)件友方装备变为活跃状态。如果它是一件武装，则你可以选择将其卸除",
            RegexOptions.CultureInvariant);
        if (battlefieldConquerReadyEquipmentMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.BattlefieldConquerReadyEquipment,
                TriggerTimings.BattlefieldConquered,
                segment,
                "Battlefield conquered ready-equipment trigger parsed for B4 routing; execution is available as an auto-resolution representative path when engine support reads BehaviorSpec.Triggers.",
                TargetScope: TriggerTargetScopes.FriendlyEquipment,
                EquipmentReadyCount: ParseChineseNumber(battlefieldConquerReadyEquipmentMatch.Groups[1].Value),
                DetachesArmament: true);
        }

        var battlefieldConquerPayCreateGoldMatch = Regex.Match(
            segment,
            @"当你征服此处时，你可以选择支付\{\{([0-9一两二三四五六七八九十]+)\}\}，以此打出([0-9一两二三四五六七八九十]+)个休眠的“([^”]+)”装备指示物",
            RegexOptions.CultureInvariant);
        if (battlefieldConquerPayCreateGoldMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.BattlefieldConquerPayCreateGold,
                TriggerTimings.BattlefieldConquered,
                segment,
                "Battlefield conquered pay-create-gold trigger parsed for B4 routing; execution is available as an auto-resolution representative path when engine support reads BehaviorSpec.Triggers.",
                ManaCost: ParseChineseNumber(battlefieldConquerPayCreateGoldMatch.Groups[1].Value),
                CreatedTokenCount: ParseChineseNumber(battlefieldConquerPayCreateGoldMatch.Groups[2].Value),
                CreatedTokenName: battlefieldConquerPayCreateGoldMatch.Groups[3].Value,
                CreatedTokenDestination: TriggerTokenDestinations.OwnerBase,
                CreatedTokenExhausted: true);
        }

        var battlefieldConquerPayReturnUnitCreateSandSoldierMatch = Regex.Match(
            segment,
            @"当你征服此处时，你可以选择支付\{\{([0-9一两二三四五六七八九十]+)\}\}并让你在此处控制的一名单位返回其所属的手牌，以此在此处打出一名([0-9一两二三四五六七八九十]+)\{\{S\}\}的“([^”]+)”",
            RegexOptions.CultureInvariant);
        if (battlefieldConquerPayReturnUnitCreateSandSoldierMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.BattlefieldConquerPayReturnUnitCreateSandSoldier,
                TriggerTimings.BattlefieldConquered,
                segment,
                "Battlefield conquered pay-return-unit-create-token trigger parsed for B4 routing; execution is available as an auto-resolution representative path when engine support reads BehaviorSpec.Triggers.",
                TargetScope: TriggerTargetScopes.ControlledUnitAtThisBattlefield,
                ManaCost: ParseChineseNumber(battlefieldConquerPayReturnUnitCreateSandSoldierMatch.Groups[1].Value),
                ReturnCount: 1,
                ReturnOriginZone: TriggerZones.Battlefield,
                ReturnDestinationZone: TriggerZones.Hand,
                CreatedTokenCount: 1,
                CreatedTokenName: battlefieldConquerPayReturnUnitCreateSandSoldierMatch.Groups[3].Value,
                CreatedTokenPower: ParseChineseNumber(battlefieldConquerPayReturnUnitCreateSandSoldierMatch.Groups[2].Value),
                CreatedTokenDestination: TriggerTokenDestinations.Battlefield,
                CreatedTokenExhausted: false);
        }

        var battlefieldConquerPayReadyLegendMatch = Regex.Match(
            segment,
            @"当你征服此处时，你可以选择支付\{\{([0-9一两二三四五六七八九十]+)\}\}，以此让你的传奇变为活跃状态",
            RegexOptions.CultureInvariant);
        if (battlefieldConquerPayReadyLegendMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.BattlefieldConquerPayReadyLegend,
                TriggerTimings.BattlefieldConquered,
                segment,
                "Battlefield conquered pay-ready-legend trigger parsed for B4 routing; execution is available as an auto-resolution representative path when engine support reads BehaviorSpec.Triggers.",
                TargetScope: TriggerTargetScopes.ControlledLegend,
                ManaCost: ParseChineseNumber(battlefieldConquerPayReadyLegendMatch.Groups[1].Value),
                LegendReadyCount: 1);
        }

        var legendConquestPayReadySelfMatch = Regex.Match(
            segment,
            @"当你征服一处战场时，你可以选择支付\{\{([0-9一两二三四五六七八九十]+)\}\}，以此让我变为活跃状态。?$",
            RegexOptions.CultureInvariant);
        if (legendConquestPayReadySelfMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.LegendConquestPayReadySelf,
                TriggerTimings.BattlefieldConquered,
                segment,
                "Legend conquest pay-ready-self trigger parsed for legend-trigger routing; execution is available through shared legend conquest TriggerSpec resolution.",
                TargetScope: TriggerTargetScopes.SourceLegend,
                ManaCost: ParseChineseNumber(legendConquestPayReadySelfMatch.Groups[1].Value),
                LegendReadyCount: 1,
                ReadiesSource: true);
        }

        var legendConquestReadySelfMatch = Regex.Match(
            segment,
            @"当你征服一处战场时，让我变为活跃状态。?$",
            RegexOptions.CultureInvariant);
        if (legendConquestReadySelfMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.LegendConquestReadySelf,
                TriggerTimings.BattlefieldConquered,
                segment,
                "Legend conquest ready-self trigger parsed for legend-trigger routing; execution is available through shared legend conquest TriggerSpec resolution.",
                TargetScope: TriggerTargetScopes.SourceLegend,
                LegendReadyCount: 1,
                ReadiesSource: true);
        }

        var legendConquestOverkillExhaustReadyUnitMatch = Regex.Match(
            segment,
            @"当你征服一处战场时，如果你给敌方单位分配了不低于([0-9一两二三四五六七八九十]+)点的过量伤害，则你可以选择让我变为休眠状态，以此让一名单位变为活跃状态。?$",
            RegexOptions.CultureInvariant);
        if (legendConquestOverkillExhaustReadyUnitMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.LegendConquestOverkillExhaustReadyUnit,
                TriggerTimings.BattlefieldConquered,
                segment,
                "Legend conquest overkill exhaust-ready-unit trigger parsed for legend-trigger routing; execution is available through shared legend conquest TriggerSpec resolution.",
                TargetScope: TriggerTargetScopes.ExhaustedUnitOnField,
                RequiredOverkillDamage: ParseChineseNumber(legendConquestOverkillExhaustReadyUnitMatch.Groups[1].Value),
                ExhaustsSource: true,
                UnitReadyCount: 1,
                Optional: true);
        }

        var battlefieldConquerOverkillWarhawkMatch = Regex.Match(
            segment,
            @"当你征服此处时，如果你给敌方单位分配了不低于([0-9一两二三四五六七八九十]+)点的过量伤害，则打出一名([0-9一两二三四五六七八九十]+)\{\{S\}\}“([^”]+)”，它拥有\{\{([^}]+)\}\}",
            RegexOptions.CultureInvariant);
        if (battlefieldConquerOverkillWarhawkMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.BattlefieldConquerOverkillCreateWarhawk,
                TriggerTimings.BattlefieldConquered,
                segment,
                "Battlefield conquered overkill create-Warhawk trigger parsed for B4 routing; execution is available as an auto-resolution representative path when engine support reads BehaviorSpec.Triggers.",
                RequiredOverkillDamage: ParseChineseNumber(battlefieldConquerOverkillWarhawkMatch.Groups[1].Value),
                CreatedTokenCount: 1,
                CreatedTokenName: battlefieldConquerOverkillWarhawkMatch.Groups[3].Value,
                CreatedTokenPower: ParseChineseNumber(battlefieldConquerOverkillWarhawkMatch.Groups[2].Value),
                CreatedTokenDestination: TriggerTokenDestinations.Battlefield,
                CreatedTokenKeywords: [battlefieldConquerOverkillWarhawkMatch.Groups[4].Value]);
        }

        var battlefieldTurnStartDamageUnitsMatch = Regex.Match(
            segment,
            @"在每名玩家各自的开始阶段开始时，对此处的所有单位造成([0-9一两二三四五六七八九十]+)点伤害",
            RegexOptions.CultureInvariant);
        if (battlefieldTurnStartDamageUnitsMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.BattlefieldTurnStartDamageAllUnits,
                TriggerTimings.TurnStart,
                segment,
                "Battlefield turn-start damage-units trigger parsed for B4 routing; execution is available when engine support reads BehaviorSpec.Triggers.",
                TargetScope: TriggerTargetScopes.UnitAtThisBattlefield,
                DamageAmount: ParseChineseNumber(battlefieldTurnStartDamageUnitsMatch.Groups[1].Value));
        }

        var battlefieldTurnStartDestroyDrawMatch = Regex.Match(
            segment,
            @"在你的开始阶段开始时，你可以选择摧毁([0-9一两二三四五六七八九十]+)名此处由你控制的单位，以此抽([0-9一两二三四五六七八九十]+)张牌",
            RegexOptions.CultureInvariant);
        if (battlefieldTurnStartDestroyDrawMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.BattlefieldTurnStartDestroyUnitDraw,
                TriggerTimings.TurnStart,
                segment,
                "Battlefield turn-start destroy-draw trigger parsed for B4 routing; execution is available as an auto-resolution representative path when engine support reads BehaviorSpec.Triggers.",
                TargetScope: TriggerTargetScopes.ControlledUnitAtThisBattlefield,
                DrawCount: ParseChineseNumber(battlefieldTurnStartDestroyDrawMatch.Groups[2].Value),
                DestroyCount: ParseChineseNumber(battlefieldTurnStartDestroyDrawMatch.Groups[1].Value),
                Optional: true);
        }

        var battlefieldFirstTurnExtraRuneMatch = Regex.Match(
            segment,
            @"每名玩家在各自的第一个回合开始阶段，额外召出([0-9一两二三四五六七八九十]+)枚符文",
            RegexOptions.CultureInvariant);
        if (battlefieldFirstTurnExtraRuneMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.BattlefieldFirstTurnExtraRune,
                TriggerTimings.TurnStart,
                segment,
                "Battlefield first-turn extra-rune trigger parsed for B4 routing; execution is available when engine support reads BehaviorSpec.Triggers.",
                TargetScope: TriggerTargetScopes.EachPlayer,
                RuneCallCount: ParseChineseNumber(battlefieldFirstTurnExtraRuneMatch.Groups[1].Value),
                FirstTurnOnly: true);
        }

        var battlefieldFirstTurnScoreMatch = Regex.Match(
            segment,
            @"每名玩家在各自的第一个回合开始阶段，获得([0-9一两二三四五六七八九十]+)分",
            RegexOptions.CultureInvariant);
        if (battlefieldFirstTurnScoreMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.BattlefieldFirstTurnScore,
                TriggerTimings.TurnStart,
                segment,
                "Battlefield first-turn score trigger parsed for B4 routing; execution is available when engine support reads BehaviorSpec.Triggers.",
                TargetScope: TriggerTargetScopes.EachPlayer,
                FirstTurnOnly: true,
                ScoreAmount: ParseChineseNumber(battlefieldFirstTurnScoreMatch.Groups[1].Value));
        }

        var battlefieldHeldPayPowerScoreMatch = Regex.Match(
            segment,
            @"当你据守此处时，你可以选择支付((?:\{\{A\}\})+)，以此额外获得([0-9一两二三四五六七八九十]+)分",
            RegexOptions.CultureInvariant);
        if (battlefieldHeldPayPowerScoreMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.BattlefieldHeldPayPowerScore,
                TriggerTimings.BattlefieldHeld,
                segment,
                "Battlefield held pay-power score trigger parsed for B4 routing; execution is available when engine support reads BehaviorSpec.Triggers.",
                PowerCost: ParsePowerCostSymbols(battlefieldHeldPayPowerScoreMatch.Groups[1].Value),
                ScoreAmount: ParseChineseNumber(battlefieldHeldPayPowerScoreMatch.Groups[2].Value),
                Optional: true);
        }

        if (segment.Contains("当你据守此处时", StringComparison.Ordinal)
            && segment.Contains("激活此处所有单位的征服效果", StringComparison.Ordinal))
        {
            return new TriggerSpec(
                TriggerKinds.BattlefieldHeldActivateUnitConquestEffects,
                TriggerTimings.BattlefieldHeld,
                segment,
                "Battlefield held activate-unit-conquest-effects trigger parsed for B4 routing; execution is available as an auto-resolution representative path when engine support reads BehaviorSpec.Triggers.",
                TargetScope: TriggerTargetScopes.UnitAtThisBattlefield);
        }

        if (segment.Contains("当你据守此处时", StringComparison.Ordinal)
            && segment.Contains("下一个法术获得等同于其基础费用的{{回响}}", StringComparison.Ordinal))
        {
            return new TriggerSpec(
                TriggerKinds.BattlefieldHeldNextSpellEcho,
                TriggerTimings.BattlefieldHeld,
                segment,
                "Battlefield held next-spell Echo parsed for B4 routing; execution remains gated until engine support reads BehaviorSpec.Triggers.",
                Duration: TriggerDurations.UntilEndOfTurn);
        }

        var heldUnitCostIncreaseMatch = Regex.Match(
            segment,
            @"当你据守此处时，你的非指示物单位在本回合内的打出费用增加\{\{(\d+)\}\}",
            RegexOptions.CultureInvariant);
        if (heldUnitCostIncreaseMatch.Success
            && int.TryParse(heldUnitCostIncreaseMatch.Groups[1].Value, out var manaDelta))
        {
            return new TriggerSpec(
                TriggerKinds.BattlefieldHeldUnitCostIncrease,
                TriggerTimings.BattlefieldHeld,
                segment,
                "Battlefield held non-token unit cost increase parsed for B4 routing; execution is available through engine support that reads BehaviorSpec.Triggers.",
                Duration: TriggerDurations.UntilEndOfTurn,
                ManaDelta: manaDelta);
        }

        var battlefieldHeldDrawMatch = Regex.Match(
            segment,
            @"当你据守此处时，抽一张牌",
            RegexOptions.CultureInvariant);
        if (battlefieldHeldDrawMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.BattlefieldHeldDrawOne,
                TriggerTimings.BattlefieldHeld,
                segment,
                "Battlefield held draw-one trigger parsed for B4 routing; execution remains gated until engine support reads BehaviorSpec.Triggers.",
                DrawCount: 1);
        }

        var battlefieldHeldCallRuneMatch = Regex.Match(
            segment,
            @"当你据守此处时，你可以选择召出一枚休眠的符文",
            RegexOptions.CultureInvariant);
        if (battlefieldHeldCallRuneMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.BattlefieldHeldCallRune,
                TriggerTimings.BattlefieldHeld,
                segment,
                "Battlefield held call-rune trigger parsed for B4 routing; execution remains gated until engine support reads BehaviorSpec.Triggers.",
                RuneCallCount: 1);
        }

        var battlefieldHeldEachPlayerCallRuneMatch = Regex.Match(
            segment,
            @"当你据守此处时，每名玩家召出一枚休眠的符文",
            RegexOptions.CultureInvariant);
        if (battlefieldHeldEachPlayerCallRuneMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.BattlefieldHeldEachPlayerCallRune,
                TriggerTimings.BattlefieldHeld,
                segment,
                "Battlefield held each-player call-rune trigger parsed for B4 routing; execution remains gated until engine support reads BehaviorSpec.Triggers.",
                TargetScope: TriggerTargetScopes.EachPlayer,
                RuneCallCount: 1);
        }

        var battlefieldHeldMoveUnitToBaseMatch = Regex.Match(
            segment,
            @"当你据守此处时，你可以选择将战场上的一名单位移动到其基地",
            RegexOptions.CultureInvariant);
        if (battlefieldHeldMoveUnitToBaseMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.BattlefieldHeldMoveUnitToBase,
                TriggerTimings.BattlefieldHeld,
                segment,
                "Battlefield held move-unit-to-base trigger parsed for B4 routing; execution remains gated until engine support reads BehaviorSpec.Triggers.",
                TargetScope: TriggerTargetScopes.UnitAtThisBattlefield,
                MoveCount: 1,
                MoveDestination: TriggerMoveDestinations.OwnerBase);
        }

        var battlefieldDefendMoveFriendlyUnitToBaseMatch = Regex.Match(
            segment,
            @"当你防守此处时，你可以选择将此处的一名友方单位移动到基地",
            RegexOptions.CultureInvariant);
        if (battlefieldDefendMoveFriendlyUnitToBaseMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.BattlefieldDefendMoveFriendlyUnitToBase,
                TriggerTimings.BattlefieldDefended,
                segment,
                "Battlefield defend move-friendly-unit-to-base trigger parsed for B4 routing; execution remains gated until engine support reads BehaviorSpec.Triggers.",
                TargetScope: TriggerTargetScopes.FriendlyUnitAtThisBattlefield,
                MoveCount: 1,
                MoveDestination: TriggerMoveDestinations.OwnerBase,
                Optional: true);
        }

        var battlefieldDefendGrantSteadfastMatch = Regex.Match(
            segment,
            @"当你防守此处时，选择一名单位，使其在本次战斗期间获得\{\{坚守([0-9一两二三四五六七八九十]+)\}\}",
            RegexOptions.CultureInvariant);
        if (battlefieldDefendGrantSteadfastMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.BattlefieldDefendGrantSteadfast,
                TriggerTimings.BattlefieldDefended,
                segment,
                "Battlefield defend grant-Steadfast trigger parsed for B4 routing; execution remains gated until engine support reads BehaviorSpec.Triggers.",
                TargetScope: TriggerTargetScopes.DefenderUnitAtThisBattlefield,
                GrantedKeyword: "坚守",
                KeywordBonus: ParseChineseNumber(battlefieldDefendGrantSteadfastMatch.Groups[1].Value));
        }

        var battlefieldHeldGrantBoonMatch = Regex.Match(
            segment,
            @"当你据守此处时，给予此处的一名单位增益",
            RegexOptions.CultureInvariant);
        if (battlefieldHeldGrantBoonMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.BattlefieldHeldGrantBoon,
                TriggerTimings.BattlefieldHeld,
                segment,
                "Battlefield held grant-boon trigger parsed for B4 routing; execution remains gated until engine support reads BehaviorSpec.Triggers.",
                TargetScope: TriggerTargetScopes.UnitAtThisBattlefield,
                BoonCount: 1);
        }

        var battlefieldHeldCreateMinionMatch = Regex.Match(
            segment,
            @"当你据守此处时，打出一名(\d+)\{\{S\}\}的“([^”]+)”到你的基地",
            RegexOptions.CultureInvariant);
        if (battlefieldHeldCreateMinionMatch.Success
            && int.TryParse(battlefieldHeldCreateMinionMatch.Groups[1].Value, out var tokenPower))
        {
            return new TriggerSpec(
                TriggerKinds.BattlefieldHeldCreateMinion,
                TriggerTimings.BattlefieldHeld,
                segment,
                "Battlefield held create-minion trigger parsed for B4 routing; execution is available when engine support reads BehaviorSpec.Triggers.",
                CreatedTokenCount: 1,
                CreatedTokenName: battlefieldHeldCreateMinionMatch.Groups[2].Value,
                CreatedTokenPower: tokenPower,
                CreatedTokenDestination: TriggerTokenDestinations.OwnerBase);
        }

        var battlefieldHeldReturnHeroMatch = Regex.Match(
            segment,
            @"当你据守此处时，如果你的英雄区域已无英雄单位牌，则可以选择让该英雄从废牌堆中返回英雄区域",
            RegexOptions.CultureInvariant);
        if (battlefieldHeldReturnHeroMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.BattlefieldHeldReturnHero,
                TriggerTimings.BattlefieldHeld,
                segment,
                "Battlefield held return-hero trigger parsed for B4 routing; execution is available when engine support reads BehaviorSpec.Triggers.",
                TargetScope: TriggerTargetScopes.OwnedHeroUnitInGraveyard,
                ReturnCount: 1,
                RequiredEmptyZone: TriggerZones.Champion,
                ReturnOriginZone: TriggerZones.Graveyard,
                ReturnDestinationZone: TriggerZones.Champion,
                ReturnCardFilter: TriggerCardFilters.TagPrefix + "CARD_CATEGORY:英雄单位");
        }

        var battlefieldHeldSevenUnitsWinMatch = Regex.Match(
            segment,
            @"当你据守此处，且在此拥有至少([0-9一两二三四五六七八九十]+)名单位时，你赢得游戏胜利",
            RegexOptions.CultureInvariant);
        if (battlefieldHeldSevenUnitsWinMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.BattlefieldHeldSevenUnitsWin,
                TriggerTimings.BattlefieldHeld,
                segment,
                "Battlefield held seven-units victory trigger parsed for B4 routing; execution is available when engine support reads BehaviorSpec.Triggers.",
                TargetScope: TriggerTargetScopes.ControlledUnitsAtThisBattlefield,
                RequiredUnitCount: ParseChineseNumber(battlefieldHeldSevenUnitsWinMatch.Groups[1].Value),
                WinsGame: true);
        }

        var friendlySpellDrawMatch = Regex.Match(
            segment,
            @"每回合首次：当你对此处的友方单位使用法术时，抽([0-9一两二三四五六七八九十]+)张牌",
            RegexOptions.CultureInvariant);
        if (friendlySpellDrawMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.BattlefieldFriendlySpellDraw,
                TriggerTimings.BattlefieldFriendlySpellTargeted,
                segment,
                "Battlefield first friendly spell targeting trigger parsed for B4 routing; execution remains gated until engine support reads BehaviorSpec.Triggers.",
                TargetScope: TriggerTargetScopes.FriendlyUnitAtThisBattlefield,
                DrawCount: ParseChineseNumber(friendlySpellDrawMatch.Groups[1].Value));
        }

        var playUnitPayBoonMatch = Regex.Match(
            segment,
            @"当一名玩家在此处打出一名单位时，该玩家可以选择支付\{\{(\d+)\}\}，以此给予该单位\{\{增益\}\}",
            RegexOptions.CultureInvariant);
        if (playUnitPayBoonMatch.Success
            && int.TryParse(playUnitPayBoonMatch.Groups[1].Value, out var playUnitBoonManaCost))
        {
            return new TriggerSpec(
                TriggerKinds.BattlefieldPlayUnitPayBoon,
                TriggerTimings.BattlefieldUnitPlayed,
                segment,
                "Battlefield unit-play pay-mana boon trigger parsed for B4 routing; execution remains gated until engine support reads BehaviorSpec.Triggers.",
                TargetScope: TriggerTargetScopes.PlayedUnitAtThisBattlefield,
                ManaCost: playUnitBoonManaCost,
                BoonCount: 1);
        }

        var unitReturnedPayCallRuneMatch = Regex.Match(
            segment,
            @"当此处的一名单位返回到一名玩家的手牌时，该玩家可以选择支付\{\{(\d+)\}\}，以此召出一枚休眠的符文",
            RegexOptions.CultureInvariant);
        if (unitReturnedPayCallRuneMatch.Success
            && int.TryParse(unitReturnedPayCallRuneMatch.Groups[1].Value, out var unitReturnedManaCost))
        {
            return new TriggerSpec(
                TriggerKinds.BattlefieldUnitReturnedPayCallRune,
                TriggerTimings.BattlefieldUnitReturned,
                segment,
                "Battlefield returned-unit pay-mana call-rune trigger parsed for B4 routing; execution remains gated until engine support reads BehaviorSpec.Triggers.",
                TargetScope: TriggerTargetScopes.ReturnedUnitAtThisBattlefield,
                ManaCost: unitReturnedManaCost,
                RuneCallCount: 1);
        }

        var firstUnitPlayedMoveOtherToBaseMatch = Regex.Match(
            segment,
            @"每回合首次，当玩家在此处打出一名非指示物单位时，该玩家可以选择将自己在此处控制的另一名单位移动到其基地",
            RegexOptions.CultureInvariant);
        if (firstUnitPlayedMoveOtherToBaseMatch.Success)
        {
            return new TriggerSpec(
                TriggerKinds.BattlefieldFirstUnitPlayedMoveOtherToBase,
                TriggerTimings.BattlefieldUnitPlayed,
                segment,
                "Battlefield first non-token unit-play move-other-to-base trigger parsed for B4 routing; execution remains gated until engine support reads BehaviorSpec.Triggers.",
                TargetScope: TriggerTargetScopes.OtherControlledUnitAtThisBattlefield,
                MoveCount: 1,
                MoveDestination: TriggerMoveDestinations.OwnerBase,
                OncePerTurn: true,
                ExcludesTokens: true);
        }

        var spellPowerBonusMatch = Regex.Match(
            segment,
            @"当一名玩家打出法术时，该玩家可以选择让自己在此处控制的一名单位在本回合内\{\{S\}\}\+(\d+)",
            RegexOptions.CultureInvariant);
        if (spellPowerBonusMatch.Success
            && int.TryParse(spellPowerBonusMatch.Groups[1].Value, out var spellPowerDelta))
        {
            return new TriggerSpec(
                TriggerKinds.BattlefieldSpellPowerBonus,
                TriggerTimings.BattlefieldSpellPlayed,
                segment,
                "Battlefield spell-play power modifier parsed for B4 routing; execution remains gated until engine support reads BehaviorSpec.Triggers.",
                TargetScope: TriggerTargetScopes.FriendlyUnitAtThisBattlefield,
                PowerDelta: spellPowerDelta,
                Duration: TriggerDurations.UntilEndOfTurn);
        }

        var highCostSpellInsightMatch = Regex.Match(
            segment,
            @"当你打出一张法术牌时，如果消耗了不低于\{\{(\d+)\}\}法力，则进行\{\{洞察\}\}",
            RegexOptions.CultureInvariant);
        if (highCostSpellInsightMatch.Success
            && int.TryParse(highCostSpellInsightMatch.Groups[1].Value, out var minimumPaidMana))
        {
            return new TriggerSpec(
                TriggerKinds.BattlefieldHighCostSpellInsightRecycle,
                TriggerTimings.BattlefieldSpellPlayed,
                segment,
                "Battlefield high-cost spell insight recycle parsed for B4 routing; execution remains gated until engine support reads BehaviorSpec.Triggers.",
                MinimumPaidMana: minimumPaidMana,
                RecycleCount: 1);
        }

        var unitSpellPlayedPowerMatch = Regex.Match(
            segment,
            @"每当你打出(?:一张法术牌|一个法术)时，让我本回合内\{\{S\}\}\+(\d+)",
            RegexOptions.CultureInvariant);
        if (unitSpellPlayedPowerMatch.Success
            && int.TryParse(unitSpellPlayedPowerMatch.Groups[1].Value, out var spellPlayedPowerDelta))
        {
            return new TriggerSpec(
                TriggerKinds.UnitSpellPlayedPowerModifier,
                TriggerTimings.BattlefieldSpellPlayed,
                segment,
                "Unit spell-play power modifier trigger parsed for spell-play trigger routing; execution is available through shared spell-play TriggerSpec resolution.",
                TargetScope: TriggerTargetScopes.SourceUnit,
                PowerDelta: spellPlayedPowerDelta,
                Duration: TriggerDurations.UntilEndOfTurn);
        }

        var unitHighCostSpellPowerMatch = Regex.Match(
            segment,
            @"每当你打出费用不低于\{\{(\d+)\}\}的法术时，让我本回合内\{\{S\}\}\+(\d+)",
            RegexOptions.CultureInvariant);
        if (unitHighCostSpellPowerMatch.Success
            && int.TryParse(unitHighCostSpellPowerMatch.Groups[1].Value, out var unitMinimumPaidMana)
            && int.TryParse(unitHighCostSpellPowerMatch.Groups[2].Value, out var unitPowerDelta))
        {
            return new TriggerSpec(
                TriggerKinds.UnitHighCostSpellPowerModifier,
                TriggerTimings.BattlefieldSpellPlayed,
                segment,
                "Unit high-cost spell power modifier trigger parsed for spell-play trigger routing; execution is available through shared spell-play TriggerSpec resolution.",
                TargetScope: TriggerTargetScopes.SourceUnit,
                PowerDelta: unitPowerDelta,
                Duration: TriggerDurations.UntilEndOfTurn,
                MinimumPaidMana: unitMinimumPaidMana);
        }

        var legendHighCostSpellDrawMatch = Regex.Match(
            segment,
            @"每当你打出一张费用不低于\{\{(\d+)\}\}的法术时，抽一张牌",
            RegexOptions.CultureInvariant);
        if (legendHighCostSpellDrawMatch.Success
            && int.TryParse(legendHighCostSpellDrawMatch.Groups[1].Value, out var legendMinimumPaidMana))
        {
            return new TriggerSpec(
                TriggerKinds.LegendHighCostSpellDrawOne,
                TriggerTimings.BattlefieldSpellPlayed,
                segment,
                "Legend high-cost spell draw trigger parsed for spell-play trigger routing; execution is available through shared spell-play TriggerSpec resolution.",
                MinimumPaidMana: legendMinimumPaidMana,
                DrawCount: 1);
        }

        var legendHighCostSpellBanishCompletionMatch = Regex.Match(
            segment,
            @"当你打出一个法术时，如果消耗了不低于\{\{(\d+)\}\}法力，则你可以选择将该法术放逐。如果以此方法放逐了(.+)张法术牌，则将这些法术牌放入各自的废牌堆，召出(.+)枚符文，并抽一张牌",
            RegexOptions.CultureInvariant);
        if (legendHighCostSpellBanishCompletionMatch.Success
            && int.TryParse(legendHighCostSpellBanishCompletionMatch.Groups[1].Value, out var legendBanishMinimumPaidMana))
        {
            return new TriggerSpec(
                TriggerKinds.LegendHighCostSpellBanishCompletion,
                TriggerTimings.BattlefieldSpellPlayed,
                segment,
                "Legend high-cost spell banish completion trigger parsed for spell-play trigger routing; execution keeps the current representative auto-resolution while optional prompt breadth remains residual.",
                TargetScope: TriggerTargetScopes.SourceLegend,
                MinimumPaidMana: legendBanishMinimumPaidMana,
                BanishCount: ParseChineseNumber(legendHighCostSpellBanishCompletionMatch.Groups[2].Value),
                RuneCallCount: ParseChineseNumber(legendHighCostSpellBanishCompletionMatch.Groups[3].Value),
                DrawCount: 1,
                Optional: true);
        }

        var movedUnitPowerMatch = Regex.Match(
            segment,
            @"每当一名单位从此处向别处移动时，让其本回合内\{\{S\}\}\+(\d+)",
            RegexOptions.CultureInvariant);
        if (movedUnitPowerMatch.Success
            && int.TryParse(movedUnitPowerMatch.Groups[1].Value, out var powerDelta))
        {
            return new TriggerSpec(
                TriggerKinds.BattlefieldUnitMovedAwayPowerModifier,
                TriggerTimings.BattlefieldUnitMovedAway,
                segment,
                "Battlefield moved-unit power modifier parsed for B4 routing; execution remains gated until engine support reads BehaviorSpec.Triggers.",
                TriggerTargetScopes.MovedUnit,
                powerDelta,
                TriggerDurations.UntilEndOfTurn);
        }

        return new TriggerSpec(
            DetermineKind(segment),
            DetermineTiming(segment),
            segment,
            "Parsed trigger candidate; queue ordering remains a later rule-domain implementation.");
    }

    private static string UnitLastBreathCreateBaseUnitKind(
        string tokenName,
        int tokenCount,
        int tokenPower,
        IReadOnlyList<string> tokenKeywords)
    {
        if (string.Equals(tokenName, "随从", StringComparison.Ordinal)
            && tokenCount == 3
            && tokenPower == 1
            && tokenKeywords.Count == 0)
        {
            return TriggerKinds.UnitLastBreathCreateMinions;
        }

        if (string.Equals(tokenName, "机器人", StringComparison.Ordinal)
            && tokenCount == 2
            && tokenPower == 3
            && tokenKeywords.Count == 0)
        {
            return TriggerKinds.UnitLastBreathCreateRobots;
        }

        if (string.Equals(tokenName, "战鹰", StringComparison.Ordinal)
            && tokenCount == 1
            && tokenPower == 1
            && tokenKeywords.Contains("法盾", StringComparer.Ordinal))
        {
            return TriggerKinds.UnitLastBreathCreateWarhawk;
        }

        return string.Empty;
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

    private static int ParsePowerCostSymbols(string raw)
    {
        return Regex.Matches(raw, @"\{\{A\}\}", RegexOptions.CultureInvariant).Count;
    }

    private static bool TryParsePowerTrait(string raw, out string trait)
    {
        trait = raw.Trim() switch
        {
            "红色" => "red",
            "绿色" => "green",
            "蓝色" => "blue",
            "橙色" => "orange",
            "紫色" => "purple",
            "黄色" => "yellow",
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(trait);
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
    private const int PowerfulUnitPowerThreshold = 5;

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

        foreach (var segment in TargetParser.SplitRulesText(text))
        {
            if (segment.Contains("你的指示物以活跃状态进场", StringComparison.Ordinal))
            {
                staticSpecs.Add(new StaticAbilitySpec(
                    StaticAbilityKinds.FriendlyFilteredUnitsEnterReady,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Friendly filtered-token active-entry static ability parsed for spec-driven token-entry routing.",
                    TargetFilter: StaticAuraTargetFilters.Token));
                continue;
            }

            if (segment.Contains("当我在场上时", StringComparison.Ordinal)
                && segment.Contains("其他友方单位以活跃状态进场", StringComparison.Ordinal))
            {
                staticSpecs.Add(new StaticAbilitySpec(
                    StaticAbilityKinds.OtherFriendlyUnitsEnterReady,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Other-friendly active-entry static ability parsed for spec-driven unit-entry routing."));
                continue;
            }

            var friendlyLevelUnitsEnterReadyMatch = Regex.Match(
                segment,
                @"\{\{等级(\d+)>\}\}\s*你的单位以活跃状态进场。?$",
                RegexOptions.CultureInvariant);
            if (friendlyLevelUnitsEnterReadyMatch.Success
                && int.TryParse(friendlyLevelUnitsEnterReadyMatch.Groups[1].Value, out var requiredPlayerExperience))
            {
                staticSpecs.Add(new StaticAbilitySpec(
                    StaticAbilityKinds.FriendlyUnitsEnterReady,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Level-gated friendly unit active-entry static ability parsed for spec-driven unit-entry routing.",
                    RequiredPlayerExperience: requiredPlayerExperience));
                continue;
            }

            var sourceUnitLevelEnterReadyMatch = Regex.Match(
                segment,
                @"\{\{等级(\d+)>\}\}\s*我.*以活跃状态进场。?$",
                RegexOptions.CultureInvariant);
            if (sourceUnitLevelEnterReadyMatch.Success
                && int.TryParse(sourceUnitLevelEnterReadyMatch.Groups[1].Value, out var sourceUnitRequiredExperience))
            {
                staticSpecs.Add(new StaticAbilitySpec(
                    StaticAbilityKinds.SourceUnitEnterReady,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Level-gated source-unit active-entry static ability parsed for spec-driven unit-entry routing.",
                    RequiredPlayerExperience: sourceUnitRequiredExperience));
                continue;
            }

            var sourceUnitMaxHandEnterReadyMatch = Regex.Match(
                segment,
                @"如果你的手牌不超过(?<max>[0-9一两二三四五六七八九十]+)张，则我以活跃状态进场。?$",
                RegexOptions.CultureInvariant);
            if (sourceUnitMaxHandEnterReadyMatch.Success)
            {
                staticSpecs.Add(new StaticAbilitySpec(
                    StaticAbilityKinds.SourceUnitEnterReady,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Source-unit active-entry static ability parsed for spec-driven unit-entry routing.",
                    MaxControllerHandCount: ParseChineseNumber(sourceUnitMaxHandEnterReadyMatch.Groups["max"].Value)));
                continue;
            }

            var sourceUnitOtherControlledTaggedUnitEnterReadyMatch = Regex.Match(
                segment,
                @"如果你控制着其他“(?<tag>[^”]+)”(?:属性)?单位，则我以活跃状态进场。?$",
                RegexOptions.CultureInvariant);
            if (sourceUnitOtherControlledTaggedUnitEnterReadyMatch.Success)
            {
                staticSpecs.Add(new StaticAbilitySpec(
                    StaticAbilityKinds.SourceUnitEnterReady,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Source-unit active-entry static ability parsed for spec-driven controlled-tag unit requirements.",
                    RequiredOtherControlledUnitTag: sourceUnitOtherControlledTaggedUnitEnterReadyMatch.Groups["tag"].Value.Trim()));
                continue;
            }

            if (Regex.IsMatch(
                    segment,
                    @"^如果本回合内有单位被摧毁，则我以活跃状态进场。?$",
                    RegexOptions.CultureInvariant))
            {
                staticSpecs.Add(new StaticAbilitySpec(
                    StaticAbilityKinds.SourceUnitEnterReady,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Source-unit active-entry static ability parsed for spec-driven unit-destroyed-this-turn requirements.",
                    RequiresUnitDestroyedThisTurn: true));
                continue;
            }

            if (Regex.IsMatch(
                    segment,
                    @"^如果对手已控制任意战场，则我以活跃状态进场。?$",
                    RegexOptions.CultureInvariant))
            {
                staticSpecs.Add(new StaticAbilitySpec(
                    StaticAbilityKinds.SourceUnitEnterReady,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Source-unit active-entry static ability parsed for spec-driven opponent battlefield requirements.",
                    RequiredOpponentControlledBattlefieldCount: 1));
                continue;
            }

            if (Regex.IsMatch(
                    segment,
                    @"^我以活跃状态进场。?$",
                    RegexOptions.CultureInvariant))
            {
                staticSpecs.Add(new StaticAbilitySpec(
                    StaticAbilityKinds.SourceUnitEnterReady,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Unconditional source-unit active-entry static ability parsed for spec-driven unit-entry routing."));
                continue;
            }

            if (segment.Contains("无法变为活跃状态", StringComparison.Ordinal)
                || segment.Contains("不能变为活跃状态", StringComparison.Ordinal))
            {
                staticSpecs.Add(new StaticAbilitySpec(
                    StaticAbilityKinds.UnitCannotBecomeActive,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Unit cannot-become-active static ability parsed for spec-driven ready prevention."));
                continue;
            }

            var unitPowerfulSelfKeywordsMatch = Regex.Match(
                segment,
                @"如果我变为\{\{强力\}\}单位，则我获得(?<keywords>.+?)(?:。?（战力达到(?<threshold>[0-9一两二三四五六七八九十]+)或以上时，即为强力单位。）)?。?$",
                RegexOptions.CultureInvariant);
            if (unitPowerfulSelfKeywordsMatch.Success)
            {
                var grantedKeywords = ParseGrantedKeywords(unitPowerfulSelfKeywordsMatch.Groups["keywords"].Value);
                if (grantedKeywords.Count > 0)
                {
                    var threshold = unitPowerfulSelfKeywordsMatch.Groups["threshold"].Success
                        ? ParseChineseNumber(unitPowerfulSelfKeywordsMatch.Groups["threshold"].Value)
                        : PowerfulUnitPowerThreshold;
                    staticSpecs.Add(new StaticAbilitySpec(
                        StaticAbilityKinds.UnitPowerfulSelfKeywords,
                        segment,
                        BehaviorImplementationStatuses.Unimplemented,
                        "Unit powerful-threshold self keyword static ability parsed for spec-driven keyword grant routing.",
                        RequiredPowerThreshold: threshold,
                        GrantedKeywords: grantedKeywords));
                    continue;
                }
            }

            var battlefieldEchoCostReductionMatch = Regex.Match(
                segment,
                @"友方\{\{回响\}\}的费用减少\{\{(\d+)\}\}",
                RegexOptions.CultureInvariant);
            if (battlefieldEchoCostReductionMatch.Success
                && int.TryParse(battlefieldEchoCostReductionMatch.Groups[1].Value, out var echoCostReductionAmount))
            {
                staticSpecs.Add(new StaticAbilitySpec(
                    StaticAbilityKinds.BattlefieldEchoCostReduction,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Battlefield Echo cost-reduction static ability parsed for B4 routing; execution is available when engine support reads BehaviorSpec.StaticAbilities.",
                    echoCostReductionAmount));
                continue;
            }

            var battlefieldEquipmentCostReductionMatch = Regex.Match(
                segment,
                @"第一件友方装备的费用减少\{\{(\d+)\}\}",
                RegexOptions.CultureInvariant);
            if (battlefieldEquipmentCostReductionMatch.Success
                && int.TryParse(battlefieldEquipmentCostReductionMatch.Groups[1].Value, out var equipmentCostReductionAmount))
            {
                staticSpecs.Add(new StaticAbilitySpec(
                    StaticAbilityKinds.BattlefieldEquipmentCostReduction,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Battlefield equipment cost-reduction static ability parsed for B4 routing; execution is available when engine support reads BehaviorSpec.StaticAbilities.",
                    equipmentCostReductionAmount));
                continue;
            }

            var battlefieldGrantUnitExperienceMatch = Regex.Match(
                segment,
                @"此处的单位获得“\{\{横置\}\}：获得(\d+)经验",
                RegexOptions.CultureInvariant);
            if (battlefieldGrantUnitExperienceMatch.Success
                && int.TryParse(battlefieldGrantUnitExperienceMatch.Groups[1].Value, out var experienceAmount))
            {
                staticSpecs.Add(new StaticAbilitySpec(
                    StaticAbilityKinds.BattlefieldGrantUnitExperienceAbility,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Battlefield granted unit experience activated ability parsed for B4 routing; execution is available when engine support reads BehaviorSpec.StaticAbilities.",
                    experienceAmount));
                continue;
            }

            var battlefieldTargetDamageBonusMatch = Regex.Match(
                segment,
                @"以此处的单位作为目标的法术或技能，造成的伤害\+(\d+)",
                RegexOptions.CultureInvariant);
            if (battlefieldTargetDamageBonusMatch.Success
                && int.TryParse(battlefieldTargetDamageBonusMatch.Groups[1].Value, out var damageBonusAmount))
            {
                staticSpecs.Add(new StaticAbilitySpec(
                    StaticAbilityKinds.BattlefieldTargetSpellSkillDamageBonus,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Battlefield target spell/skill damage bonus parsed for B4 routing; execution is available when engine support reads BehaviorSpec.StaticAbilities.",
                    damageBonusAmount));
                continue;
            }

            if (segment.Contains("所有友方传奇获得", StringComparison.Ordinal)
                && segment.Contains("将你控制的一件武装贴附到你控制的一名单位", StringComparison.Ordinal))
            {
                staticSpecs.Add(new StaticAbilitySpec(
                    StaticAbilityKinds.BattlefieldGrantLegendAttachArmament,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Battlefield granted legend attach-armament activated ability parsed for B4 routing; execution is available when engine support reads BehaviorSpec.StaticAbilities."));
                continue;
            }

            if (segment.Contains("单位无法从此处移动到基地", StringComparison.Ordinal))
            {
                staticSpecs.Add(new StaticAbilitySpec(
                    StaticAbilityKinds.BattlefieldPreventMoveToBase,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Battlefield prevent-move-to-base static ability parsed for B4 routing; execution remains gated until engine support reads BehaviorSpec.StaticAbilities."));
                continue;
            }

            if (segment.Contains("单位无法被打出到此处", StringComparison.Ordinal))
            {
                staticSpecs.Add(new StaticAbilitySpec(
                    StaticAbilityKinds.BattlefieldPreventUnitPlay,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Battlefield prevent-unit-play static ability parsed for B4 routing; execution remains gated until engine support reads BehaviorSpec.StaticAbilities."));
                continue;
            }

            var battlefieldScoreDelayMatch = Regex.Match(
                segment,
                @"每名玩家在各自的第([0-9一两二三四五六七八九十]+)回合开始前，无法从此处获得分数",
                RegexOptions.CultureInvariant);
            if (battlefieldScoreDelayMatch.Success)
            {
                staticSpecs.Add(new StaticAbilitySpec(
                    StaticAbilityKinds.BattlefieldScoreDelayUntilTurn,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Battlefield score-delay static ability parsed for B4 routing; execution is available when engine support reads BehaviorSpec.StaticAbilities.",
                    ParseChineseNumber(battlefieldScoreDelayMatch.Groups[1].Value)));
                continue;
            }

            var battlefieldWinningScoreIncreaseMatch = Regex.Match(
                segment,
                @"使赢得游戏所需的分数\+([0-9一两二三四五六七八九十]+)",
                RegexOptions.CultureInvariant);
            if (battlefieldWinningScoreIncreaseMatch.Success)
            {
                staticSpecs.Add(new StaticAbilitySpec(
                    StaticAbilityKinds.BattlefieldWinningScoreIncrease,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Battlefield winning-score increase static ability parsed for B4 routing; execution is available when engine support reads BehaviorSpec.StaticAbilities.",
                    ParseChineseNumber(battlefieldWinningScoreIncreaseMatch.Groups[1].Value)));
                continue;
            }

            var battlefieldDestroyedInBattleRecallMatch = Regex.Match(
                segment,
                @"如果此处的一名单位在战斗中被摧毁，其控制者可以选择支付(?<cost>(?:\{\{A\}\})+)，以此改为移除其所受伤害、将其变为休眠状态、并将其召回",
                RegexOptions.CultureInvariant);
            if (battlefieldDestroyedInBattleRecallMatch.Success)
            {
                staticSpecs.Add(new StaticAbilitySpec(
                    StaticAbilityKinds.BattlefieldDestroyedInBattlePayRecallReplacement,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Battlefield battle-destroyed pay-power recall replacement parsed for B4 routing; execution is available when engine support reads BehaviorSpec.StaticAbilities.",
                    ParsePowerCostSymbols(battlefieldDestroyedInBattleRecallMatch.Groups["cost"].Value)));
                continue;
            }

            if (segment.Contains("你可以选择在此处额外布置一张{{待命}}卡牌", StringComparison.Ordinal))
            {
                staticSpecs.Add(new StaticAbilitySpec(
                    StaticAbilityKinds.BattlefieldExtraStandbyDestination,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Battlefield extra-standby destination static ability parsed for B4 routing; execution is available when engine support reads BehaviorSpec.StaticAbilities."));
            }
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

    private static IReadOnlyList<string> ParseGrantedKeywords(string raw)
    {
        return Regex.Matches(raw, @"\{\{([^}]+)\}\}", RegexOptions.CultureInvariant)
            .Select(match => match.Groups[1].Value.Trim())
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
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

    private static int ParsePowerCostSymbols(string raw)
    {
        return Regex.Matches(raw, @"\{\{A\}\}", RegexOptions.CultureInvariant).Count;
    }
}

public static class StaticAuraParser
{
    private const string RuleTextLayer = "RULE_TEXT";
    private const string StaticAuraLayer = "STATIC_AURA";

    public static IReadOnlyList<StaticAuraSpec> Parse(string text)
    {
        var auras = new List<StaticAuraSpec>();
        foreach (var segment in TargetParser.SplitRulesText(text))
        {
            var sourceObjectLevelPowerMatch = Regex.Match(
                segment,
                @"\{\{等级(\d+)>\}\}\s*我获得\{\{S\}\}\+(\d+)",
                RegexOptions.CultureInvariant);
            if (sourceObjectLevelPowerMatch.Success
                && int.TryParse(sourceObjectLevelPowerMatch.Groups[1].Value, out var sourceObjectLevelExperience)
                && int.TryParse(sourceObjectLevelPowerMatch.Groups[2].Value, out var sourceObjectLevelPowerDelta))
            {
                auras.Add(new StaticAuraSpec(
                    StaticAuraKinds.SourceObjectPower,
                    StaticAuraLayer,
                    "WHILE_SOURCE_ON_PUBLIC_FIELD",
                    StaticAuraTargetScopes.SourceObject,
                    StaticAuraParticipantScopes.SourceObject,
                    sourceObjectLevelPowerDelta,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Static aura parsed for B1 routing; execution is available when combat power calculation reads BehaviorSpec.StaticAuras.",
                    RequiredPlayerExperience: sourceObjectLevelExperience));
                continue;
            }

            var sourceObjectFilteredPowerMatch = Regex.Match(
                segment,
                @"如果我拥有([^，。]+)，则我(?:额外)?获得\{\{S\}\}\+(\d+)",
                RegexOptions.CultureInvariant);
            if (sourceObjectFilteredPowerMatch.Success
                && int.TryParse(sourceObjectFilteredPowerMatch.Groups[2].Value, out var sourceObjectFilteredPowerDelta))
            {
                var tag = sourceObjectFilteredPowerMatch.Groups[1].Value
                    .Replace("{{", string.Empty, StringComparison.Ordinal)
                    .Replace("}}", string.Empty, StringComparison.Ordinal)
                    .Trim();
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    auras.Add(new StaticAuraSpec(
                        StaticAuraKinds.SourceObjectFilteredPower,
                        StaticAuraLayer,
                        "WHILE_SOURCE_ON_PUBLIC_FIELD",
                        StaticAuraTargetScopes.SourceObject,
                        StaticAuraParticipantScopes.SourceObject,
                        sourceObjectFilteredPowerDelta,
                        segment,
                        BehaviorImplementationStatuses.Unimplemented,
                        "Static aura parsed for B1 routing; execution remains gated until engine support reads BehaviorSpec.StaticAuras.",
                        StaticAuraTargetFilters.TagPrefix + tag));
                    continue;
                }
            }

            var sourceSameLocationOtherFriendlyUnitPowerMatch = Regex.Match(
                segment,
                @"如果你在此处有其他单位，则我获得\{\{S\}\}\+(\d+)",
                RegexOptions.CultureInvariant);
            if (sourceSameLocationOtherFriendlyUnitPowerMatch.Success
                && int.TryParse(sourceSameLocationOtherFriendlyUnitPowerMatch.Groups[1].Value, out var sameLocationPowerDelta))
            {
                auras.Add(new StaticAuraSpec(
                    StaticAuraKinds.SourceSameLocationOtherFriendlyUnitPower,
                    StaticAuraLayer,
                    "WHILE_SOURCE_ON_PUBLIC_FIELD",
                    StaticAuraTargetScopes.SourceObject,
                    StaticAuraParticipantScopes.SameLocationOtherFriendlyPublicUnits,
                    sameLocationPowerDelta,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Static aura parsed for B1 routing; execution remains gated until engine support reads BehaviorSpec.StaticAuras.",
                    RequiredParticipantCount: 1));
                continue;
            }

            var friendlySingleDefendingUnitPowerMatch = Regex.Match(
                segment,
                @"如果你只有一名友方单位防守一处战场，则该单位\{\{S\}\}\+(\d+)",
                RegexOptions.CultureInvariant);
            if (friendlySingleDefendingUnitPowerMatch.Success
                && int.TryParse(friendlySingleDefendingUnitPowerMatch.Groups[1].Value, out var singleDefenderPowerDelta))
            {
                auras.Add(new StaticAuraSpec(
                    StaticAuraKinds.FriendlySingleDefendingUnitPower,
                    StaticAuraLayer,
                    "WHILE_SINGLE_FRIENDLY_UNIT_DEFENDING_BATTLEFIELD",
                    StaticAuraTargetScopes.FriendlySingleDefendingBattlefieldUnit,
                    StaticAuraParticipantScopes.SingleFriendlyDefendingBattlefieldUnit,
                    singleDefenderPowerDelta,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Combat static aura parsed for B1 routing; execution is available when combat power calculation reads BehaviorSpec.StaticAuras.",
                    RequiredDefendingUnitCount: 1));
                continue;
            }

            var friendlyLevelUnitsPowerMatch = Regex.Match(
                segment,
                @"\{\{等级(\d+)>\}\}\s*你的单位获得\{\{S\}\}\+(\d+)",
                RegexOptions.CultureInvariant);
            if (friendlyLevelUnitsPowerMatch.Success
                && int.TryParse(friendlyLevelUnitsPowerMatch.Groups[1].Value, out var requiredPlayerExperience)
                && int.TryParse(friendlyLevelUnitsPowerMatch.Groups[2].Value, out var friendlyUnitsPowerDelta))
            {
                auras.Add(new StaticAuraSpec(
                    StaticAuraKinds.FriendlyUnitsPower,
                    StaticAuraLayer,
                    "WHILE_SOURCE_AND_TARGET_ON_PUBLIC_FIELD",
                    StaticAuraTargetScopes.FriendlyUnits,
                    StaticAuraParticipantScopes.FriendlyPublicUnits,
                    friendlyUnitsPowerDelta,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Static aura parsed for B1 routing; execution is available when combat power calculation reads BehaviorSpec.StaticAuras.",
                    RequiredPlayerExperience: requiredPlayerExperience));
                continue;
            }

            var sourceAttackingWithAnotherUnitPowerMatch = Regex.Match(
                segment,
                @"如果我和另一名单位一起进攻一处战场，则我获得\{\{S\}\}\+(\d+)",
                RegexOptions.CultureInvariant);
            if (sourceAttackingWithAnotherUnitPowerMatch.Success
                && int.TryParse(sourceAttackingWithAnotherUnitPowerMatch.Groups[1].Value, out var attackingPowerDelta))
            {
                auras.Add(new StaticAuraSpec(
                    StaticAuraKinds.SourceAttackingWithAnotherUnitPower,
                    StaticAuraLayer,
                    "WHILE_SOURCE_ATTACKING_WITH_REQUIRED_ATTACKER_COUNT",
                    StaticAuraTargetScopes.SourceObject,
                    StaticAuraParticipantScopes.AttackingBattlefieldPublicUnits,
                    attackingPowerDelta,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Combat static aura parsed for B1 routing; execution is available when combat power calculation reads BehaviorSpec.StaticAuras.",
                    RequiredAttackingUnitCount: 2));
                continue;
            }

            var sourceLoneBattlePowerMatch = Regex.Match(
                segment,
                @"如果我独自进攻或防守一处战场，则我获得\s*\{\{S\}\}\+(\d+)",
                RegexOptions.CultureInvariant);
            if (sourceLoneBattlePowerMatch.Success
                && int.TryParse(sourceLoneBattlePowerMatch.Groups[1].Value, out var loneBattlePowerDelta))
            {
                auras.Add(new StaticAuraSpec(
                    StaticAuraKinds.SourceLoneBattlePower,
                    StaticAuraLayer,
                    "WHILE_SOURCE_ATTACKING_OR_DEFENDING_ALONE",
                    StaticAuraTargetScopes.SourceObject,
                    StaticAuraParticipantScopes.BattlefieldPublicUnits,
                    loneBattlePowerDelta,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Combat static aura parsed for B1 routing; execution is available when combat power calculation reads BehaviorSpec.StaticAuras.",
                    RequiredAttackingUnitCount: 1,
                    RequiredDefendingUnitCount: 1));
                continue;
            }

            var sourceAttackingReadyEnemyUnitPowerMatch = Regex.Match(
                segment,
                @"当我进攻时，如果此处有处于活跃状态的敌方单位，则让我\{\{S\}\}\+(\d+)",
                RegexOptions.CultureInvariant);
            if (sourceAttackingReadyEnemyUnitPowerMatch.Success
                && int.TryParse(sourceAttackingReadyEnemyUnitPowerMatch.Groups[1].Value, out var readyEnemyPowerDelta))
            {
                auras.Add(new StaticAuraSpec(
                    StaticAuraKinds.SourceAttackingReadyEnemyUnitPower,
                    StaticAuraLayer,
                    "WHILE_SOURCE_ATTACKING_READY_ENEMY_UNIT_BATTLEFIELD",
                    StaticAuraTargetScopes.SourceObject,
                    StaticAuraParticipantScopes.ReadyEnemyBattlefieldPublicUnits,
                    readyEnemyPowerDelta,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Combat static aura parsed for B1 routing; execution is available when combat power calculation reads BehaviorSpec.StaticAuras.",
                    RequiredReadyEnemyUnitCount: 1));
                continue;
            }

            var brushBattlefieldFilteredPowerMatch = Regex.Match(
                segment,
                @"此处的“鸟类”、“猫科”、“犬形”、“魄罗”属性单位和艾翁单位获得\{\{S\}\}\+(\d+)",
                RegexOptions.CultureInvariant);
            if (brushBattlefieldFilteredPowerMatch.Success
                && int.TryParse(brushBattlefieldFilteredPowerMatch.Groups[1].Value, out var brushPowerDelta))
            {
                auras.Add(new StaticAuraSpec(
                    StaticAuraKinds.BattlefieldFilteredUnitsPower,
                    StaticAuraLayer,
                    "WHILE_SOURCE_BATTLEFIELD_AND_PARTICIPANT_AT_BATTLEFIELD",
                    StaticAuraTargetScopes.SameBattlefieldFilteredUnits,
                    StaticAuraParticipantScopes.SameBattlefieldFilteredPublicUnits,
                    brushPowerDelta,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Static aura parsed for B1 routing; execution remains gated until engine support reads BehaviorSpec.StaticAuras.",
                    StaticAuraTargetFilters.AnyPrefix
                    + StaticAuraTargetFilters.TagPrefix + "鸟类"
                    + "|"
                    + StaticAuraTargetFilters.TagPrefix + "猫科"
                    + "|"
                    + StaticAuraTargetFilters.TagPrefix + "犬形"
                    + "|"
                    + StaticAuraTargetFilters.TagPrefix + "魄罗"
                    + "|"
                    + StaticAuraTargetFilters.CardNamePrefix + "艾翁"));
                continue;
            }

            var battlefieldAllUnitsKeywordMatch = Regex.Match(
                segment,
                @"此处的单位获得\{\{([^}]+)\}\}(?!\+)",
                RegexOptions.CultureInvariant);
            if (battlefieldAllUnitsKeywordMatch.Success)
            {
                var grantedKeyword = battlefieldAllUnitsKeywordMatch.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(grantedKeyword)
                    && !string.Equals(grantedKeyword, "S", StringComparison.Ordinal))
                {
                    auras.Add(new StaticAuraSpec(
                        StaticAuraKinds.BattlefieldAllUnitsKeyword,
                        RuleTextLayer,
                        "WHILE_SOURCE_BATTLEFIELD_AND_PARTICIPANT_AT_BATTLEFIELD",
                        StaticAuraTargetScopes.SameBattlefieldUnits,
                        StaticAuraParticipantScopes.SameBattlefieldPublicUnits,
                        0,
                        segment,
                        BehaviorImplementationStatuses.Unimplemented,
                        "Static keyword aura parsed for B2 routing; execution is available when the source card reaches a supported battlefield rule-domain path.",
                        GrantedKeyword: grantedKeyword));
                    continue;
                }
            }

            var battlefieldFilteredKeywordMatch = Regex.Match(
                segment,
                @"此处拥有(?:\{\{)?([^}，。]+)(?:\}\})?的单位获得\{\{([^}]+)\}\}(?!\+)",
                RegexOptions.CultureInvariant);
            if (battlefieldFilteredKeywordMatch.Success)
            {
                var targetTag = battlefieldFilteredKeywordMatch.Groups[1].Value.Trim();
                var grantedKeyword = battlefieldFilteredKeywordMatch.Groups[2].Value.Trim();
                if (!string.IsNullOrWhiteSpace(targetTag)
                    && !string.IsNullOrWhiteSpace(grantedKeyword)
                    && !string.Equals(grantedKeyword, "S", StringComparison.Ordinal))
                {
                    auras.Add(new StaticAuraSpec(
                        StaticAuraKinds.BattlefieldFilteredUnitsKeyword,
                        RuleTextLayer,
                        "WHILE_SOURCE_BATTLEFIELD_AND_PARTICIPANT_AT_BATTLEFIELD",
                        StaticAuraTargetScopes.SameBattlefieldFilteredUnits,
                        StaticAuraParticipantScopes.SameBattlefieldFilteredPublicUnits,
                        0,
                        segment,
                        BehaviorImplementationStatuses.Unimplemented,
                        "Static keyword aura parsed for B2 routing; execution remains gated until engine support reads BehaviorSpec.StaticAuras.",
                        StaticAuraTargetFilters.TagPrefix + targetTag,
                        GrantedKeyword: grantedKeyword));
                    continue;
                }
            }

            var battlefieldIsolatedDefenderKeywordModifierMatch = Regex.Match(
                segment,
                @"如果防守此处的单位落单，则该单位\{\{S\}\}-(\d+)",
                RegexOptions.CultureInvariant);
            if (battlefieldIsolatedDefenderKeywordModifierMatch.Success
                && int.TryParse(battlefieldIsolatedDefenderKeywordModifierMatch.Groups[1].Value, out var isolatedDefenderPenalty))
            {
                auras.Add(new StaticAuraSpec(
                    StaticAuraKinds.BattlefieldIsolatedDefenderKeywordModifier,
                    RuleTextLayer,
                    "WHILE_SOURCE_BATTLEFIELD_AND_PARTICIPANT_AT_BATTLEFIELD",
                    StaticAuraTargetScopes.SameBattlefieldIsolatedDefender,
                    StaticAuraParticipantScopes.SameBattlefieldIsolatedDefender,
                    -isolatedDefenderPenalty,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Battlefield isolated-defender keyword modifier parsed for B2 routing; execution remains gated until engine support reads BehaviorSpec.StaticAuras.",
                    GrantedKeyword: "坚守"));
                continue;
            }

            var sameBattlefieldFriendlyFilteredCountSourcePowerMatch = Regex.Match(
                segment,
                @"我所处的战场(?:你)?每有一名拥有(?:\{\{)?([^}，。]+)(?:\}\})?的(?:友方)?单位，我便获得\{\{S\}\}\+(\d+)",
                RegexOptions.CultureInvariant);
            if (sameBattlefieldFriendlyFilteredCountSourcePowerMatch.Success
                && int.TryParse(sameBattlefieldFriendlyFilteredCountSourcePowerMatch.Groups[2].Value, out var sameBattlefieldCountPowerDelta))
            {
                var targetTag = sameBattlefieldFriendlyFilteredCountSourcePowerMatch.Groups[1].Value.Trim();
                auras.Add(new StaticAuraSpec(
                    StaticAuraKinds.SameBattlefieldFriendlyFilteredUnitCountToSourcePower,
                    StaticAuraLayer,
                    "WHILE_SOURCE_ON_PUBLIC_FIELD",
                    StaticAuraTargetScopes.SourceObject,
                    StaticAuraParticipantScopes.SameBattlefieldFriendlyFilteredPublicUnits,
                    sameBattlefieldCountPowerDelta,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Static aura parsed for B1 routing; execution remains gated until engine support reads BehaviorSpec.StaticAuras.",
                    StaticAuraTargetFilters.TagPrefix + targetTag));
                continue;
            }

            var conditionalSameBattlefieldOtherFriendlyKeywordMatch = Regex.Match(
                segment,
                @"如果我位于战场上，则你此处的其他单位获得(?<grants>[^。（]+)",
                RegexOptions.CultureInvariant);
            if (conditionalSameBattlefieldOtherFriendlyKeywordMatch.Success
                && TryAddSameBattlefieldOtherFriendlyUnitsKeywordAuras(
                    auras,
                    segment,
                    conditionalSameBattlefieldOtherFriendlyKeywordMatch.Groups["grants"].Value))
            {
                continue;
            }

            var sameBattlefieldOtherFriendlyKeywordMatch = Regex.Match(
                segment,
                @"此处的其他友方单位获得(?<grants>[^。（]+)",
                RegexOptions.CultureInvariant);
            if (sameBattlefieldOtherFriendlyKeywordMatch.Success
                && TryAddSameBattlefieldOtherFriendlyUnitsKeywordAuras(
                    auras,
                    segment,
                    sameBattlefieldOtherFriendlyKeywordMatch.Groups["grants"].Value))
            {
                continue;
            }

            var sameBattlefieldOtherFriendlyPowerMatch = Regex.Match(
                segment,
                @"此处的其他友方单位获得\{\{S\}\}\+(\d+)",
                RegexOptions.CultureInvariant);
            if (sameBattlefieldOtherFriendlyPowerMatch.Success
                && int.TryParse(sameBattlefieldOtherFriendlyPowerMatch.Groups[1].Value, out var otherFriendlyPowerDelta))
            {
                auras.Add(new StaticAuraSpec(
                    StaticAuraKinds.SameBattlefieldOtherFriendlyUnitsPowerPlusOne,
                    StaticAuraLayer,
                    "WHILE_SOURCE_AND_TARGET_AT_SAME_BATTLEFIELD",
                    StaticAuraTargetScopes.SameBattlefieldOtherFriendlyUnits,
                    StaticAuraParticipantScopes.SameBattlefieldOtherFriendlyPublicUnits,
                    otherFriendlyPowerDelta,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Static aura parsed for B1 routing; execution remains gated until engine support reads BehaviorSpec.StaticAuras."));
                continue;
            }

            var otherFriendlyPowerMatch = Regex.Match(
                segment,
                @"其他友方单位获得\{\{S\}\}\+(\d+)",
                RegexOptions.CultureInvariant);
            if (otherFriendlyPowerMatch.Success
                && int.TryParse(otherFriendlyPowerMatch.Groups[1].Value, out var globalOtherFriendlyPowerDelta))
            {
                auras.Add(new StaticAuraSpec(
                    StaticAuraKinds.OtherFriendlyUnitsPower,
                    StaticAuraLayer,
                    "WHILE_SOURCE_AND_TARGET_ON_PUBLIC_FIELD",
                    StaticAuraTargetScopes.OtherFriendlyUnits,
                    StaticAuraParticipantScopes.OtherFriendlyPublicUnits,
                    globalOtherFriendlyPowerDelta,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Static aura parsed for B1 routing; execution remains gated until engine support reads BehaviorSpec.StaticAuras."));
                continue;
            }

            var otherFriendlyKeywordMatch = Regex.Match(
                segment,
                @"其他友方单位获得\{\{([^}]+)\}\}(?!\+)",
                RegexOptions.CultureInvariant);
            if (otherFriendlyKeywordMatch.Success)
            {
                var grantedKeyword = otherFriendlyKeywordMatch.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(grantedKeyword)
                    && !string.Equals(grantedKeyword, "S", StringComparison.Ordinal))
                {
                    auras.Add(new StaticAuraSpec(
                        StaticAuraKinds.OtherFriendlyUnitsKeyword,
                        RuleTextLayer,
                        "WHILE_SOURCE_AND_TARGET_ON_PUBLIC_FIELD",
                        StaticAuraTargetScopes.OtherFriendlyUnits,
                        StaticAuraParticipantScopes.OtherFriendlyPublicUnits,
                        0,
                        segment,
                        BehaviorImplementationStatuses.Unimplemented,
                        "Static keyword aura parsed for B2 routing; execution remains gated until engine support reads BehaviorSpec.StaticAuras.",
                        GrantedKeyword: grantedKeyword));
                    continue;
                }
            }

            var sameBattlefieldOtherFriendlyBoonPowerMatch = Regex.Match(
                segment,
                @"我所在战场上其他拥有增益的友方单位获得\{\{S\}\}\+(\d+)",
                RegexOptions.CultureInvariant);
            if (sameBattlefieldOtherFriendlyBoonPowerMatch.Success
                && int.TryParse(sameBattlefieldOtherFriendlyBoonPowerMatch.Groups[1].Value, out var sameBattlefieldBoonPowerDelta))
            {
                auras.Add(new StaticAuraSpec(
                    StaticAuraKinds.SameBattlefieldOtherFriendlyFilteredUnitsPower,
                    StaticAuraLayer,
                    "WHILE_SOURCE_AND_TARGET_AT_SAME_BATTLEFIELD",
                    StaticAuraTargetScopes.SameBattlefieldOtherFriendlyFilteredUnits,
                    StaticAuraParticipantScopes.SameBattlefieldOtherFriendlyFilteredPublicUnits,
                    sameBattlefieldBoonPowerDelta,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Static aura parsed for B1 routing; execution remains gated until engine support reads BehaviorSpec.StaticAuras.",
                    StaticAuraTargetFilters.TagPrefix + "增益"));
                continue;
            }

            var friendlyTokenUnitsKeywordMatch = Regex.Match(
                segment,
                @"你的指示物单位获得(?<grants>[^。（]+)",
                RegexOptions.CultureInvariant);
            if (friendlyTokenUnitsKeywordMatch.Success)
            {
                if (TryAddFriendlyFilteredUnitsKeywordAuras(
                        auras,
                        segment,
                        StaticAuraTargetFilters.UnitToken,
                        friendlyTokenUnitsKeywordMatch.Groups["grants"].Value))
                {
                    continue;
                }
            }

            var friendlyTaggedUnitsKeywordMatch = Regex.Match(
                segment,
                @"你的“([^”]+)”属性单位获得(?<grants>[^。（]+)",
                RegexOptions.CultureInvariant);
            if (friendlyTaggedUnitsKeywordMatch.Success)
            {
                var targetTag = friendlyTaggedUnitsKeywordMatch.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(targetTag)
                    && TryAddFriendlyFilteredUnitsKeywordAuras(
                        auras,
                        segment,
                        StaticAuraTargetFilters.TagPrefix + targetTag,
                        friendlyTaggedUnitsKeywordMatch.Groups["grants"].Value))
                {
                    continue;
                }
            }

            var friendlyTokenUnitsPowerMatch = Regex.Match(
                segment,
                @"你的指示物单位获得\{\{S\}\}\+(\d+)",
                RegexOptions.CultureInvariant);
            if (friendlyTokenUnitsPowerMatch.Success
                && int.TryParse(friendlyTokenUnitsPowerMatch.Groups[1].Value, out var friendlyTokenUnitsPowerDelta))
            {
                auras.Add(new StaticAuraSpec(
                    StaticAuraKinds.FriendlyFilteredUnitsPower,
                    StaticAuraLayer,
                    "WHILE_SOURCE_AND_TARGET_ON_PUBLIC_FIELD",
                    StaticAuraTargetScopes.FriendlyFilteredUnits,
                    StaticAuraParticipantScopes.FriendlyFilteredPublicUnits,
                    friendlyTokenUnitsPowerDelta,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Static aura parsed for B1 routing; execution remains gated until engine support reads BehaviorSpec.StaticAuras.",
                    StaticAuraTargetFilters.UnitToken));
                continue;
            }

            var friendlyTaggedUnitsPowerMatch = Regex.Match(
                segment,
                @"你的“([^”]+)”属性单位获得\{\{S\}\}\+(\d+)",
                RegexOptions.CultureInvariant);
            if (friendlyTaggedUnitsPowerMatch.Success
                && int.TryParse(friendlyTaggedUnitsPowerMatch.Groups[2].Value, out var friendlyTaggedUnitsPowerDelta))
            {
                auras.Add(new StaticAuraSpec(
                    StaticAuraKinds.FriendlyFilteredUnitsPower,
                    StaticAuraLayer,
                    "WHILE_SOURCE_AND_TARGET_ON_PUBLIC_FIELD",
                    StaticAuraTargetScopes.FriendlyFilteredUnits,
                    StaticAuraParticipantScopes.FriendlyFilteredPublicUnits,
                    friendlyTaggedUnitsPowerDelta,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Static aura parsed for B1 routing; execution remains gated until engine support reads BehaviorSpec.StaticAuras.",
                    StaticAuraTargetFilters.TagPrefix + friendlyTaggedUnitsPowerMatch.Groups[1].Value));
                continue;
            }

            if (segment.Contains("每有一件友方装备", StringComparison.Ordinal)
                && segment.Contains("{{S}}+1", StringComparison.Ordinal))
            {
                auras.Add(new StaticAuraSpec(
                    StaticAuraKinds.FriendlyFieldEquipmentCountToSourceUnitPower,
                    StaticAuraLayer,
                    "WHILE_SOURCE_ON_PUBLIC_FIELD",
                    StaticAuraTargetScopes.SourceObject,
                    StaticAuraParticipantScopes.FriendlyPublicFieldEquipment,
                    1,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Static aura parsed for B1 routing; execution remains gated until engine support reads BehaviorSpec.StaticAuras."));
                continue;
            }

            var battlefieldAllUnitsPowerMatch = Regex.Match(
                segment,
                @"此处的所有单位获得\{\{S\}\}\+(\d+)",
                RegexOptions.CultureInvariant);
            if (battlefieldAllUnitsPowerMatch.Success
                && int.TryParse(battlefieldAllUnitsPowerMatch.Groups[1].Value, out var battlefieldAllUnitsPowerDelta))
            {
                auras.Add(new StaticAuraSpec(
                    StaticAuraKinds.BattlefieldAllUnitsPowerPlusOne,
                    StaticAuraLayer,
                    "WHILE_SOURCE_BATTLEFIELD_AND_PARTICIPANT_AT_BATTLEFIELD",
                    StaticAuraTargetScopes.SameBattlefieldUnits,
                    StaticAuraParticipantScopes.SameBattlefieldPublicUnits,
                    battlefieldAllUnitsPowerDelta,
                    segment,
                    BehaviorImplementationStatuses.Unimplemented,
                    "Static aura parsed for B1 routing; execution remains gated until engine support reads BehaviorSpec.StaticAuras."));
            }
        }

        return auras
            .GroupBy(
                aura => $"{aura.Kind}\n{aura.TargetFilter}\n{aura.GrantedKeyword}\n{aura.PowerDeltaPerParticipant}\n{aura.Text}",
                StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    private static bool TryAddFriendlyFilteredUnitsKeywordAuras(
        List<StaticAuraSpec> auras,
        string segment,
        string targetFilter,
        string grantsText)
    {
        var added = false;
        foreach (Match keywordMatch in Regex.Matches(
            grantsText,
            @"\{\{([^}]+)\}\}(?!\+)",
            RegexOptions.CultureInvariant))
        {
            var grantedKeyword = keywordMatch.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(grantedKeyword)
                || string.Equals(grantedKeyword, "S", StringComparison.Ordinal))
            {
                continue;
            }

            auras.Add(new StaticAuraSpec(
                StaticAuraKinds.FriendlyFilteredUnitsKeyword,
                RuleTextLayer,
                "WHILE_SOURCE_AND_TARGET_ON_PUBLIC_FIELD",
                StaticAuraTargetScopes.FriendlyFilteredUnits,
                StaticAuraParticipantScopes.FriendlyFilteredPublicUnits,
                0,
                segment,
                BehaviorImplementationStatuses.Unimplemented,
                "Static keyword aura parsed for B2 routing; execution remains gated until engine support reads BehaviorSpec.StaticAuras.",
                targetFilter,
                GrantedKeyword: grantedKeyword));
            added = true;
        }

        return added;
    }

    private static bool TryAddSameBattlefieldOtherFriendlyUnitsKeywordAuras(
        List<StaticAuraSpec> auras,
        string segment,
        string grantsText)
    {
        var added = false;
        foreach (Match keywordMatch in Regex.Matches(
            grantsText,
            @"\{\{([^}]+)\}\}(?!\+)",
            RegexOptions.CultureInvariant))
        {
            var grantedKeyword = keywordMatch.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(grantedKeyword)
                || string.Equals(grantedKeyword, "S", StringComparison.Ordinal))
            {
                continue;
            }

            auras.Add(new StaticAuraSpec(
                StaticAuraKinds.SameBattlefieldOtherFriendlyUnitsKeyword,
                RuleTextLayer,
                "WHILE_SOURCE_AND_TARGET_AT_SAME_BATTLEFIELD",
                StaticAuraTargetScopes.SameBattlefieldOtherFriendlyUnits,
                StaticAuraParticipantScopes.SameBattlefieldOtherFriendlyPublicUnits,
                0,
                segment,
                BehaviorImplementationStatuses.Unimplemented,
                "Static keyword aura parsed for B2 routing; execution remains gated until engine support reads BehaviorSpec.StaticAuras.",
                GrantedKeyword: grantedKeyword));
            added = true;
        }

        return added;
    }
}

public static class EffectPhraseParser
{
    public static IReadOnlyList<EffectPhraseSpec> Parse(string text)
    {
        return ParseTemplateIds(text)
            .Select(templateId => BuildEffectPhrase(text, templateId))
            .ToArray();
    }

    public static IReadOnlyList<string> ParseTemplateIds(string text)
    {
        var templateIds = new List<string>();
        AddIf(templateIds, text, BehaviorTemplateIds.Draw, "抽");
        AddIf(templateIds, text, BehaviorTemplateIds.Damage, "伤害");
        AddIf(templateIds, text, BehaviorTemplateIds.Destroy, "摧毁");
        AddMoveIf(templateIds, text);
        AddIf(templateIds, text, BehaviorTemplateIds.Recall, "返回", "召回");
        AddIf(templateIds, text, BehaviorTemplateIds.Recycle, "回收");
        AddIf(templateIds, text, BehaviorTemplateIds.Banish, "放逐");
        AddIf(templateIds, text, BehaviorTemplateIds.Stun, "眩晕");
        AddTempMightIf(templateIds, text);
        AddIf(templateIds, text, BehaviorTemplateIds.Boon, "增益");
        AddIf(templateIds, text, BehaviorTemplateIds.GainExperience, "经验");
        AddIf(templateIds, text, BehaviorTemplateIds.Assemble, "装配", "百炼");
        AddIf(templateIds, text, BehaviorTemplateIds.Echo, "回响");
        AddIf(templateIds, text, BehaviorTemplateIds.Ambush, "伏击");
        AddIf(templateIds, text, BehaviorTemplateIds.Control, "控制权");
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

    private static void AddTempMightIf(List<string> templateIds, string text)
    {
        foreach (var segment in TargetParser.SplitRulesText(text ?? string.Empty))
        {
            var hasPowerMarker = segment.Contains("{{S}}+", StringComparison.Ordinal)
                || segment.Contains("{{S}}-", StringComparison.Ordinal)
                || segment.Contains("战力", StringComparison.Ordinal);
            if (!hasPowerMarker)
            {
                continue;
            }

            if (segment.Contains("增益", StringComparison.Ordinal)
                && !segment.Contains("战力", StringComparison.Ordinal))
            {
                continue;
            }

            templateIds.Add(BehaviorTemplateIds.TempMight);
            return;
        }
    }

    private static void AddMoveIf(List<string> templateIds, string text)
    {
        foreach (var segment in TargetParser.SplitRulesText(text ?? string.Empty))
        {
            if (!segment.Contains("移动", StringComparison.Ordinal))
            {
                continue;
            }

            var positiveMovementText = segment
                .Replace("不算作移动", string.Empty, StringComparison.Ordinal)
                .Replace("不被视为移动", string.Empty, StringComparison.Ordinal);
            if (positiveMovementText.Contains("移动", StringComparison.Ordinal))
            {
                templateIds.Add(BehaviorTemplateIds.Move);
                return;
            }
        }
    }

    private static string FirstPhraseForTemplate(string text, string templateId)
    {
        if (string.Equals(templateId, BehaviorTemplateIds.GainExperience, StringComparison.Ordinal))
        {
            return TargetParser.SplitRulesText(text)
                .FirstOrDefault(segment => IsGainExperiencePhrase(segment))
                ?? string.Empty;
        }

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
            BehaviorTemplateIds.Control => ["控制权"],
            _ => [templateId]
        };

        return TargetParser.SplitRulesText(text)
            .FirstOrDefault(segment => needles.Any(needle => segment.Contains(needle, StringComparison.Ordinal)))
            ?? string.Empty;
    }

    private static bool IsGainExperiencePhrase(string phrase)
    {
        return Regex.IsMatch(
                phrase,
                @"获得[0-9一两二三四五六七八九十]+经验",
                RegexOptions.CultureInvariant)
            && !phrase.Contains("{{狩猎}}", StringComparison.Ordinal)
            && !phrase.Contains("征服或据守", StringComparison.Ordinal);
    }

    private static EffectPhraseSpec BuildEffectPhrase(string text, string templateId)
    {
        var phrase = FirstPhraseForTemplate(text, templateId);
        var spec = new EffectPhraseSpec(
            templateId,
            phrase,
            BehaviorImplementationStatuses.Unimplemented,
            "Template parser candidate; execution is skeleton-only until explicitly mapped.");

        return templateId switch
        {
            BehaviorTemplateIds.Move => spec with
            {
                TargetScope = ResolveMoveTargetScope(phrase),
                MovesTarget = phrase.Contains("移动", StringComparison.Ordinal),
                MoveCount = ParseMoveCount(phrase),
                MoveDestination = ResolveMoveDestination(phrase)
            },
            BehaviorTemplateIds.Damage => spec with
            {
                TargetScope = ResolveUnitTargetScope(phrase),
                DamageAmount = ParseDamageAmount(phrase),
                ConditionKind = BehaviorEffectConditionKinds.None
            },
            BehaviorTemplateIds.Destroy => spec with
            {
                TargetScope = ResolveUnitTargetScope(phrase),
                DestroysTarget = phrase.Contains("摧毁", StringComparison.Ordinal)
            },
            BehaviorTemplateIds.Banish => spec with
            {
                TargetScope = ResolveUnitTargetScope(phrase),
                BanishesTarget = phrase.Contains("放逐", StringComparison.Ordinal),
                PlayDestinationZone = ResolvePlayDestinationZone(phrase),
                IgnoreCosts = phrase.Contains("无视费用", StringComparison.Ordinal)
            },
            BehaviorTemplateIds.Recall => spec with
            {
                TargetScope = ResolveUnitTargetScope(phrase),
                ReturnsTargetToHand = phrase.Contains("返回", StringComparison.Ordinal)
                    && phrase.Contains("手牌", StringComparison.Ordinal),
                ReturnDestinationZone = ResolveReturnDestinationZone(phrase)
            },
            BehaviorTemplateIds.Recycle => spec with
            {
                TargetScope = ResolveRecycleTargetScope(phrase),
                RecyclesTarget = phrase.Contains("回收", StringComparison.Ordinal),
                RecycleSourceZone = ResolveRecycleSourceZone(phrase),
                RecycleDestinationZone = ResolveRecycleDestinationZone(phrase),
                TargetForbiddenTag = ResolveTargetForbiddenTag(phrase)
            },
            BehaviorTemplateIds.Boon => spec with
            {
                TargetScope = ResolveUnitTargetScope(phrase),
                GrantsBoon = phrase.Contains("给予", StringComparison.Ordinal)
                    && phrase.Contains("增益", StringComparison.Ordinal),
                BoonPowerBonusAmount = ParsePowerModifierAmount(phrase) ?? ParsePowerModifierAmount(text)
            },
            BehaviorTemplateIds.Draw => spec with
            {
                DrawCount = ParseDrawCount(phrase),
                ConditionKind = phrase.Contains("从手牌中打出此牌", StringComparison.Ordinal)
                    ? BehaviorEffectConditionKinds.PlayedFromHand
                    : null
            },
            BehaviorTemplateIds.GainExperience => spec with
            {
                ExperienceCount = ParseExperienceCount(phrase),
                ExperienceCountFormula = ResolveExperienceCountFormula(phrase),
                ExperienceCountMultiplier = ParseExperienceCountMultiplier(phrase)
            },
            BehaviorTemplateIds.Control => spec with
            {
                TargetScope = ResolveControlTargetScope(phrase),
                GainsControl = phrase.Contains("获得", StringComparison.Ordinal)
                    && phrase.Contains("控制权", StringComparison.Ordinal),
                ControlDestinationZone = ResolveControlDestinationZone(text, phrase),
                ReadiesTarget = phrase.Contains("活跃状态", StringComparison.Ordinal)
                    || text.Contains("活跃状态", StringComparison.Ordinal),
                ExhaustsControlledTarget = phrase.Contains("休眠状态", StringComparison.Ordinal)
                    || text.Contains("休眠状态", StringComparison.Ordinal),
                ControlDuration = ResolveControlDuration(text),
                ControlReturnDestinationZone = ResolveControlReturnDestinationZone(text),
                ControlReturnCountsAsMove = ResolveControlReturnCountsAsMove(text)
            },
            BehaviorTemplateIds.Stun => spec with
            {
                TargetScope = ResolveUnitTargetScope(phrase),
                StatusEffectId = phrase.Contains("眩晕", StringComparison.Ordinal)
                    ? "STUNNED"
                    : null
            },
            BehaviorTemplateIds.TempMight => spec with
            {
                TargetScope = ResolveUnitTargetScope(phrase) ?? ResolveUnitTargetScope(text),
                PowerModifierAmount = ParsePowerModifierAmount(phrase),
                ConditionKind = ResolvePowerModifierConditionKind(phrase)
            },
            _ => spec
        };
    }

    private static int? ParseExperienceCount(string phrase)
    {
        if (phrase.Contains("每有", StringComparison.Ordinal)
            || phrase.Contains("每当", StringComparison.Ordinal))
        {
            return null;
        }

        var match = Regex.Match(
            phrase ?? string.Empty,
            @"获得(?<count>[一二两三四五六七八九十\d]+)经验",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return null;
        }

        return ParseSmallChineseNumber(match.Groups["count"].Value);
    }

    private static string? ResolveExperienceCountFormula(string phrase)
    {
        return phrase.Contains("场上每有一名友方单位", StringComparison.Ordinal)
            ? BehaviorEffectFormulaKinds.FriendlyFieldUnitCount
            : null;
    }

    private static int? ParseExperienceCountMultiplier(string phrase)
    {
        if (ResolveExperienceCountFormula(phrase) is null)
        {
            return null;
        }

        var match = Regex.Match(
            phrase ?? string.Empty,
            @"获得(?<count>[一二两三四五六七八九十\d]+)经验",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return null;
        }

        return ParseSmallChineseNumber(match.Groups["count"].Value);
    }

    private static string? ResolveControlTargetScope(string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase)
            || !phrase.Contains("单位", StringComparison.Ordinal))
        {
            return null;
        }

        if (phrase.Contains("敌方", StringComparison.Ordinal)
            && phrase.Contains("战场", StringComparison.Ordinal))
        {
            return "ENEMY_BATTLEFIELD_UNIT";
        }

        return ResolveUnitTargetScope(phrase);
    }

    private static string? ResolveControlDestinationZone(string text, string phrase)
    {
        if (phrase.Contains("召回", StringComparison.Ordinal)
            || phrase.Contains("基地", StringComparison.Ordinal))
        {
            return "BASE";
        }

        if (phrase.Contains("战场", StringComparison.Ordinal))
        {
            return "BATTLEFIELD";
        }

        if (text.Contains("获得战场上一名", StringComparison.Ordinal)
            || text.Contains("获得战场上的", StringComparison.Ordinal))
        {
            return "BATTLEFIELD";
        }

        return null;
    }

    private static string? ResolveControlDuration(string text)
    {
        return text.Contains("回合结束时", StringComparison.Ordinal)
            && (text.Contains("失去", StringComparison.Ordinal)
                || text.Contains("取回", StringComparison.Ordinal))
            ? "UNTIL_END_OF_TURN"
            : null;
    }

    private static string? ResolveControlReturnDestinationZone(string text)
    {
        return text.Contains("回合结束时", StringComparison.Ordinal)
            && (text.Contains("召回", StringComparison.Ordinal)
                || text.Contains("送回基地", StringComparison.Ordinal))
            ? "BASE"
            : null;
    }

    private static bool? ResolveControlReturnCountsAsMove(string text)
    {
        if (text.Contains("不算作移动", StringComparison.Ordinal)
            || text.Contains("不被视为移动", StringComparison.Ordinal))
        {
            return false;
        }

        return null;
    }

    private static int? ParseDamageAmount(string phrase)
    {
        var match = Regex.Match(
            phrase ?? string.Empty,
            @"造成(?<amount>[一二两三四五六七八九十\d]+)点伤害",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return null;
        }

        return ParseSmallChineseNumber(match.Groups["amount"].Value);
    }

    private static int? ParseDrawCount(string phrase)
    {
        var match = Regex.Match(
            phrase ?? string.Empty,
            @"抽(?<count>[一二两三四五六七八九十\d]+)张牌",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return null;
        }

        return ParseSmallChineseNumber(match.Groups["count"].Value);
    }

    private static int? ParsePowerModifierAmount(string phrase)
    {
        var match = Regex.Match(
            phrase ?? string.Empty,
            @"\{\{S\}\}(?<sign>[+-])(?<amount>\d+)",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return null;
        }

        var amount = int.Parse(match.Groups["amount"].Value, CultureInfo.InvariantCulture);
        return string.Equals(match.Groups["sign"].Value, "-", StringComparison.Ordinal)
            ? -amount
            : amount;
    }

    private static string? ResolvePowerModifierConditionKind(string phrase)
    {
        if (phrase.Contains("进攻方", StringComparison.Ordinal))
        {
            return "TARGET_IS_ATTACKING";
        }

        if (phrase.Contains("防守方", StringComparison.Ordinal))
        {
            return "TARGET_IS_DEFENDING";
        }

        return null;
    }

    private static string? ResolveReturnDestinationZone(string phrase)
    {
        return phrase.Contains("手牌", StringComparison.Ordinal)
            ? "HAND"
            : null;
    }

    private static string? ResolveRecycleSourceZone(string phrase)
    {
        return phrase.Contains("手牌", StringComparison.Ordinal)
            ? TriggerZones.Hand
            : null;
    }

    private static string? ResolveRecycleDestinationZone(string phrase)
    {
        return phrase.Contains("回收", StringComparison.Ordinal)
            ? TriggerZones.MainDeck
            : null;
    }

    private static int? ParseMoveCount(string phrase)
    {
        var match = Regex.Match(
            phrase ?? string.Empty,
            @"(?<count>[一二两三四五六七八九十\d]+)名",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return null;
        }

        return ParseSmallChineseNumber(match.Groups["count"].Value);
    }

    private static string? ResolveMoveDestination(string phrase)
    {
        return phrase.Contains("所属的基地", StringComparison.Ordinal)
            || phrase.Contains("移动到基地", StringComparison.Ordinal)
            || phrase.Contains("移动至基地", StringComparison.Ordinal)
                ? TriggerMoveDestinations.OwnerBase
                : null;
    }

    private static string? ResolveMoveTargetScope(string phrase)
    {
        if (!phrase.Contains("单位", StringComparison.Ordinal))
        {
            return null;
        }

        var referencesBattlefield = phrase.Contains("战场", StringComparison.Ordinal);
        if (phrase.Contains("友方单位", StringComparison.Ordinal)
            && referencesBattlefield)
        {
            return "FRIENDLY_BATTLEFIELD_UNIT";
        }

        if (phrase.Contains("敌方单位", StringComparison.Ordinal)
            && referencesBattlefield)
        {
            return "ENEMY_BATTLEFIELD_UNIT";
        }

        if (referencesBattlefield)
        {
            return "BATTLEFIELD_UNIT";
        }

        if (phrase.Contains("友方单位", StringComparison.Ordinal))
        {
            return "FRIENDLY_UNIT";
        }

        if (phrase.Contains("敌方单位", StringComparison.Ordinal))
        {
            return "ENEMY_UNIT";
        }

        return "ANY_UNIT";
    }

    private static string? ResolveRecycleTargetScope(string phrase)
    {
        if (phrase.Contains("对手", StringComparison.Ordinal)
            && phrase.Contains("手牌", StringComparison.Ordinal))
        {
            return "OPPONENT_HAND_CARD";
        }

        if (phrase.Contains("手牌", StringComparison.Ordinal))
        {
            return "ANY_HAND_CARD";
        }

        return ResolveUnitTargetScope(phrase);
    }

    private static string? ResolveTargetForbiddenTag(string phrase)
    {
        return phrase.Contains("非单位", StringComparison.Ordinal)
            ? "CARD_TYPE:UNIT"
            : null;
    }

    private static string? ResolvePlayDestinationZone(string phrase)
    {
        if (phrase.Contains("基地", StringComparison.Ordinal))
        {
            return "BASE";
        }

        if (phrase.Contains("战场", StringComparison.Ordinal))
        {
            return "BATTLEFIELD";
        }

        return null;
    }

    private static string? ResolveUnitTargetScope(string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase)
            || !phrase.Contains("单位", StringComparison.Ordinal))
        {
            return null;
        }

        if (phrase.Contains("友方战场单位", StringComparison.Ordinal))
        {
            return "FRIENDLY_BATTLEFIELD_UNIT";
        }

        if (phrase.Contains("友方单位", StringComparison.Ordinal))
        {
            return "FRIENDLY_UNIT";
        }

        if (phrase.Contains("敌方战场单位", StringComparison.Ordinal))
        {
            return "ENEMY_BATTLEFIELD_UNIT";
        }

        if (phrase.Contains("敌方单位", StringComparison.Ordinal))
        {
            return "ENEMY_UNIT";
        }

        if (phrase.Contains("战场单位", StringComparison.Ordinal)
            || phrase.Contains("战场上的", StringComparison.Ordinal)
            || phrase.Contains("战场上", StringComparison.Ordinal))
        {
            return "BATTLEFIELD_UNIT";
        }

        if (phrase.Contains("进攻单位", StringComparison.Ordinal)
            || phrase.Contains("攻击单位", StringComparison.Ordinal))
        {
            return "ATTACKING_UNIT";
        }

        return "ANY_UNIT";
    }

    private static int? ParseSmallChineseNumber(string raw)
    {
        if (int.TryParse(raw, out var value))
        {
            return value;
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
            _ => null
        };
    }
}
