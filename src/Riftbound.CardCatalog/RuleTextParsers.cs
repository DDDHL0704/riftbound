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
    public static IReadOnlyList<TriggerSpec> Parse(string text)
    {
        return TargetParser.SplitRulesText(text)
            .Where(segment => segment.Contains("当", StringComparison.Ordinal)
                || segment.Contains("每当", StringComparison.Ordinal)
                || segment.Contains("打出我时", StringComparison.Ordinal)
                || segment.Contains("回合开始", StringComparison.Ordinal)
                || segment.Contains("被摧毁", StringComparison.Ordinal)
                || segment.Contains("征服", StringComparison.Ordinal))
            .Select(ToTriggerSpec)
            .ToArray();
    }

    private static TriggerSpec ToTriggerSpec(string segment)
    {
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
                "Battlefield held non-token unit cost increase parsed for B4 routing; execution remains gated until engine support reads BehaviorSpec.Triggers.",
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

        foreach (var segment in TargetParser.SplitRulesText(text))
        {
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

            var sameBattlefieldOtherFriendlyKeywordMatch = Regex.Match(
                segment,
                @"此处的其他友方单位获得\{\{([^}]+)\}\}(?!\+)",
                RegexOptions.CultureInvariant);
            if (sameBattlefieldOtherFriendlyKeywordMatch.Success)
            {
                var grantedKeyword = sameBattlefieldOtherFriendlyKeywordMatch.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(grantedKeyword)
                    && !string.Equals(grantedKeyword, "S", StringComparison.Ordinal))
                {
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
                    continue;
                }
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
            .GroupBy(aura => $"{aura.Kind}\n{aura.Text}", StringComparer.Ordinal)
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
