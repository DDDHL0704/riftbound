using System.Text.RegularExpressions;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

internal sealed record AssembleEquipmentProfile(
    string CardNo,
    string DisplayName,
    string OptionalCost,
    string OptionalCostLabel,
    string PowerTrait,
    int PowerCost,
    string PaymentResourceReason,
    int ExperienceCost = 0,
    int RequiredGraveyardRecycleCardCount = 0,
    bool RequiresDestroyFriendlyUnitCost = false,
    int ManaCost = 0,
    bool ReduceManaCostByTargetPower = false);

internal static class AssembleEquipmentProfileCatalog
{
    private const string AssembleAnyPowerOptionalCost = "ASSEMBLE_ANY_POWER";
    private const string AssembleDynamicAnyPowerOptionalCostPrefix = "ASSEMBLE_";
    private const string AssembleDynamicAnyPowerOptionalCostSuffix = "_ANY_POWER";
    private const string SpendExperienceOptionalCostPrefix = "SPEND_EXPERIENCE:";

    private static readonly Lazy<IReadOnlyDictionary<string, AssembleEquipmentProfile>> Profiles =
        new(BuildProfiles, LazyThreadSafetyMode.ExecutionAndPublication);

    public static AssembleEquipmentProfile FallbackRepresentative =>
        Profiles.Value.Values.OrderBy(profile => profile.CardNo, StringComparer.Ordinal).FirstOrDefault()
        ?? throw new InvalidOperationException("No assemble equipment profiles were built from BehaviorSpec.");

    public static bool HasImplementedRepresentative(string? cardNo)
    {
        return TryGet(cardNo, out _);
    }

    public static bool TryGet(string? cardNo, out AssembleEquipmentProfile profile)
    {
        profile = default!;
        return !string.IsNullOrWhiteSpace(cardNo)
            && Profiles.Value.TryGetValue(cardNo.Trim(), out profile!);
    }

    private static IReadOnlyDictionary<string, AssembleEquipmentProfile> BuildProfiles()
    {
        var catalog = OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult();
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var playCardBehaviors = CardBehaviorRegistry.GetAll()
            .Select(behavior => new ImplementedCardBehavior(
                behavior.CardNo,
                behavior.EffectKind,
                behavior.DisplayName))
            .ToArray();
        var implementedBehaviors = OfficialRuleDomainBehaviorCatalog.MergeWithNonPlayCardDomains(
            catalog.Cards,
            playCardBehaviors);

        return BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, implementedBehaviors)
            .Select(TryBuildProfile)
            .Where(profile => profile is not null)
            .Select(profile => profile!)
            .ToDictionary(
                profile => profile.CardNo,
                profile => profile,
                StringComparer.Ordinal);
    }

    private static AssembleEquipmentProfile? TryBuildProfile(BehaviorSpec spec)
    {
        if (!IsImplementedEquipmentCard(spec)
            || !HasAssembleKeyword(spec)
            || !TryParseAssembleCost(spec, out var assembleCost))
        {
            return null;
        }

        return new AssembleEquipmentProfile(
            spec.CardNo,
            spec.CardName,
            assembleCost.OptionalCost,
            assembleCost.OptionalCostLabel,
            assembleCost.PowerTrait,
            assembleCost.PowerCost,
            assembleCost.PaymentResourceReason,
            assembleCost.ExperienceCost,
            RequiredGraveyardRecycleCardCount: assembleCost.RequiredGraveyardRecycleCardCount,
            RequiresDestroyFriendlyUnitCost: assembleCost.RequiresDestroyFriendlyUnitCost,
            ManaCost: assembleCost.ManaCost,
            ReduceManaCostByTargetPower: assembleCost.ReduceManaCostByTargetPower);
    }

    private static bool IsImplementedEquipmentCard(BehaviorSpec spec)
    {
        return (string.Equals(spec.CardCategoryName, "装备", StringComparison.Ordinal)
                || string.Equals(spec.CardCategoryName, "专属装备", StringComparison.Ordinal))
            && CardBehaviorRegistry.TryGetByCardNo(spec.CardNo, out var behavior)
            && behavior.PlaysSourceToBaseAsEquipment;
    }

    private static bool HasAssembleKeyword(BehaviorSpec spec)
    {
        return spec.Keywords.Any(keyword => string.Equals(
            keyword.Keyword,
            CardEquipmentKeywordNames.Assemble,
            StringComparison.Ordinal));
    }

    private static bool TryParseAssembleCost(BehaviorSpec spec, out AssembleCost assembleCost)
    {
        assembleCost = default!;
        var compactText = CompactRulesText(spec.OfficialText);
        if (string.IsNullOrWhiteSpace(compactText) || !compactText.Contains("装配", StringComparison.Ordinal))
        {
            return false;
        }

        var requiredGraveyardRecycleCardCount = RequiredGraveyardRecycleCardCount(compactText);
        var requiresDestroyFriendlyUnitCost = compactText.Contains("摧毁一名友方单位", StringComparison.Ordinal);
        var reduceManaCostByTargetPower = compactText.Contains("法力费用减少你所选单位的战力", StringComparison.Ordinal);

        if (TryParseExperienceCost(compactText, out var experienceCost))
        {
            assembleCost = new AssembleCost(
                $"{SpendExperienceOptionalCostPrefix}{experienceCost}",
                $"消耗 {experienceCost} 经验",
                string.Empty,
                0,
                "experience assemble cost",
                ExperienceCost: experienceCost,
                RequiredGraveyardRecycleCardCount: requiredGraveyardRecycleCardCount,
                RequiresDestroyFriendlyUnitCost: requiresDestroyFriendlyUnitCost);
            return true;
        }

        if (TryParseAnyPowerCost(compactText, out var anyPowerCost, out var manaCost))
        {
            var optionalCost = manaCost > 0
                ? $"{AssembleDynamicAnyPowerOptionalCostPrefix}{manaCost}{AssembleDynamicAnyPowerOptionalCostSuffix}"
                : AssembleAnyPowerOptionalCost;
            var optionalCostLabel = manaCost > 0
                ? $"装配 {manaCost} 法力 + 任意符能"
                    + (reduceManaCostByTargetPower ? "（按目标战力减费）" : string.Empty)
                : "装配任意符能";

            assembleCost = new AssembleCost(
                optionalCost,
                optionalCostLabel,
                string.Empty,
                anyPowerCost,
                "payment resource action: recycle any rune for assemble cost",
                RequiredGraveyardRecycleCardCount: requiredGraveyardRecycleCardCount,
                RequiresDestroyFriendlyUnitCost: requiresDestroyFriendlyUnitCost,
                ManaCost: manaCost,
                ReduceManaCostByTargetPower: reduceManaCostByTargetPower);
            return true;
        }

        if (TryParseTypedPowerCost(compactText, out var powerTrait, out var powerCost))
        {
            assembleCost = new AssembleCost(
                TypedPowerOptionalCost(powerTrait),
                $"装配{ChineseRuneTraitName(powerTrait)}符能",
                powerTrait,
                powerCost,
                $"payment resource action: recycle {powerTrait} rune for assemble cost",
                RequiredGraveyardRecycleCardCount: requiredGraveyardRecycleCardCount,
                RequiresDestroyFriendlyUnitCost: requiresDestroyFriendlyUnitCost);
            return true;
        }

        return false;
    }

    private static string CompactRulesText(string text)
    {
        var compact = Regex.Replace(text ?? string.Empty, @"\s+", string.Empty);
        return compact
            .Replace("{{", string.Empty, StringComparison.Ordinal)
            .Replace("}}", string.Empty, StringComparison.Ordinal)
            .Replace("：", ":", StringComparison.Ordinal);
    }

    private static bool TryParseExperienceCost(string compactText, out int experienceCost)
    {
        experienceCost = 0;
        var match = Regex.Match(compactText, @"装配.*?消耗(?<amount>[0-9一二两三四五六七八九十]+)点?经验");
        if (!match.Success)
        {
            return false;
        }

        return TryParsePositiveCount(match.Groups["amount"].Value, out experienceCost);
    }

    private static bool TryParseAnyPowerCost(string compactText, out int powerCost, out int manaCost)
    {
        powerCost = 0;
        manaCost = 0;
        var match = Regex.Match(compactText, @"装配(?<mana>[0-9]+)?A");
        if (!match.Success)
        {
            return false;
        }

        powerCost = 1;
        if (match.Groups["mana"].Success
            && int.TryParse(match.Groups["mana"].Value, out var parsedManaCost))
        {
            manaCost = parsedManaCost;
        }

        return true;
    }

    private static bool TryParseTypedPowerCost(string compactText, out string powerTrait, out int powerCost)
    {
        powerTrait = string.Empty;
        powerCost = 0;
        var match = Regex.Match(
            compactText,
            @"装配(?:—?支付)?(?<amount>[0-9]+)?(?<color>红色|绿色|蓝色|紫色|橙色|黄色)");
        if (!match.Success
            || !TryMapChineseRuneTrait(match.Groups["color"].Value, out powerTrait))
        {
            return false;
        }

        powerCost = match.Groups["amount"].Success
            && int.TryParse(match.Groups["amount"].Value, out var parsedPowerCost)
                ? parsedPowerCost
                : 1;
        return true;
    }

    private static int RequiredGraveyardRecycleCardCount(string compactText)
    {
        var match = Regex.Match(compactText, @"从你的废牌堆回收(?<amount>[0-9一二两三四五六七八九十]+)张卡牌");
        return match.Success && TryParsePositiveCount(match.Groups["amount"].Value, out var count)
            ? count
            : 0;
    }

    private static bool TryMapChineseRuneTrait(string value, out string trait)
    {
        trait = value switch
        {
            "红色" => RuneTrait.Red,
            "绿色" => RuneTrait.Green,
            "蓝色" => RuneTrait.Blue,
            "紫色" => RuneTrait.Purple,
            "橙色" => RuneTrait.Orange,
            "黄色" => RuneTrait.Yellow,
            _ => string.Empty
        };
        return !string.IsNullOrWhiteSpace(trait);
    }

    private static string TypedPowerOptionalCost(string trait)
    {
        return trait switch
        {
            RuneTrait.Red => "ASSEMBLE_RED",
            RuneTrait.Green => "ASSEMBLE_GREEN",
            RuneTrait.Blue => "ASSEMBLE_BLUE",
            RuneTrait.Purple => "ASSEMBLE_PURPLE",
            RuneTrait.Orange => "ASSEMBLE_ORANGE",
            RuneTrait.Yellow => "ASSEMBLE_YELLOW",
            _ => throw new InvalidOperationException($"Unsupported assemble rune trait '{trait}'.")
        };
    }

    private static string ChineseRuneTraitName(string trait)
    {
        return trait switch
        {
            RuneTrait.Red => "红色",
            RuneTrait.Green => "绿色",
            RuneTrait.Blue => "蓝色",
            RuneTrait.Purple => "紫色",
            RuneTrait.Orange => "橙色",
            RuneTrait.Yellow => "黄色",
            _ => throw new InvalidOperationException($"Unsupported assemble rune trait '{trait}'.")
        };
    }

    private static bool TryParsePositiveCount(string value, out int count)
    {
        count = value switch
        {
            "一" => 1,
            "二" or "两" => 2,
            "三" => 3,
            "四" => 4,
            "五" => 5,
            "六" => 6,
            "七" => 7,
            "八" => 8,
            "九" => 9,
            "十" => 10,
            _ => int.TryParse(value, out var parsed) ? parsed : 0
        };
        return count > 0;
    }

    private sealed record AssembleCost(
        string OptionalCost,
        string OptionalCostLabel,
        string PowerTrait,
        int PowerCost,
        string PaymentResourceReason,
        int ExperienceCost = 0,
        int RequiredGraveyardRecycleCardCount = 0,
        bool RequiresDestroyFriendlyUnitCost = false,
        int ManaCost = 0,
        bool ReduceManaCostByTargetPower = false);
}
