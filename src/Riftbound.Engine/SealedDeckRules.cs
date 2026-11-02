using System.Collections.Immutable;
using Riftbound.CardCatalog;

namespace Riftbound.Engine;

// Supplied only by the authoritative room, never taken from SUBMIT_DECK.
public sealed record SealedDeckValidationContext(
    IReadOnlyDictionary<string, int> Pool,
    int BestOf = 1);

public static class SealedDeckRules
{
    public const int MinimumMainDeckCount = 25;
    public const string BlankBattlefieldCardNo = "SEALED·BLANK-BATTLEFIELD";

    // A synthetic physical battlefield, excluded from the official catalog and
    // accepted by deck validation only in the sealed format (CN 602.3.d.1).
    public static OfficialCard BlankBattlefield { get; } = new(
        Id: 0, CardCategory: "battlefield", CardCategoryName: "战场",
        CardNo: BlankBattlefieldCardNo, CardName: "空白战场", SubTitle: "",
        ExtendType: null, ExtendTypeName: "", CardColorList: ImmutableArray.Create("colorless"),
        Hero: "", Region: "", Tag: "", Artist: "", CardEffect: "", FlavorText: "",
        Energy: null, ReturnEnergy: null, Power: null,
        ProductCodeList: ImmutableArray<string>.Empty, ProductNameList: ImmutableArray<string>.Empty,
        Rarity: "", RarityName: "", ExtendRarity: "", ExtendRarityName: "",
        FrontImage: "", BackImage: "", ListSort: null, Status: null, CardGroupLimit: null)
    {
        CardCategoryList = ImmutableArray.Create("battlefield"),
        CardCategoryNameList = ImmutableArray.Create("战场")
    };

    private static readonly IReadOnlyDictionary<string, string> BasicRunes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["炽烈符文"] = RuneTrait.Red, ["翠意符文"] = RuneTrait.Green,
            ["灵光符文"] = RuneTrait.Blue, ["摧破符文"] = RuneTrait.Orange,
            ["混沌符文"] = RuneTrait.Purple, ["序理符文"] = RuneTrait.Yellow
        };

    public static bool IsBasicRune(OfficialCard card) =>
        card.CardCategoryNameList.Count == 1 && card.CardCategoryNameList[0] == "符文"
        && BasicRunes.TryGetValue(card.CardName.Trim(), out var trait)
        && string.IsNullOrWhiteSpace(card.CardEffect)
        && card.CardColorList.Select(RuneTrait.Normalize).Distinct(StringComparer.Ordinal).SequenceEqual([trait]);
}

public static partial class OfficialDeckValidator
{
    private static OfficialDeckValidationResult ValidateSealed(
        OfficialDecklist submitted,
        OfficialCardCatalog catalog,
        SealedDeckValidationContext? context)
    {
        var errors = new List<string>();
        var deck = Normalize(submitted);
        var cardsByNo = catalog.Cards.ToDictionary(card => card.CardNo, StringComparer.Ordinal);
        cardsByNo[SealedDeckRules.BlankBattlefieldCardNo] = SealedDeckRules.BlankBattlefield;
        var legend = deck.LegendCardNo.Length == 0 ? null
            : RequireCard(cardsByNo, deck.LegendCardNo, "legendCardNo", errors);
        var champion = deck.ChampionCardNo.Length == 0 ? null
            : RequireCard(cardsByNo, deck.ChampionCardNo, "championCardNo", errors);
        ValidateAdditionalLegends(cardsByNo, deck.MainDeck, deck.AdditionalLegendCardNos ?? [], legend, errors);
        if (legend is not null && (legend.CardCategoryNameList.Count != 1 || legend.CardCategoryNameList[0] != "传奇"))
            errors.Add("传奇牌编号必须指向一张传奇牌。");
        if (champion is not null && (champion.CardCategoryNameList.Count != 1 || champion.CardCategoryNameList[0] != "英雄单位"))
            errors.Add("英雄牌编号必须指向一张英雄单位牌。");
        if (deck.ChampionCardNo.Length > 0 && !deck.MainDeck.Contains(deck.ChampionCardNo, StringComparer.Ordinal))
            errors.Add("主牌堆必须包含 1 张所选英雄牌；该牌开局进入英雄区。");
        if (deck.MainDeck.Count < SealedDeckRules.MinimumMainDeckCount)
            errors.Add($"主牌堆至少需要 {SealedDeckRules.MinimumMainDeckCount} 张牌（包含所选英雄）。");
        if (deck.RuneDeck.Count != RuneDeckCount)
            errors.Add($"符文牌堆必须正好包含 {RuneDeckCount} 张符文牌。");
        var bestOf = context?.BestOf ?? 1;
        if (bestOf is not (1 or 3)) errors.Add("现开比赛的 bestOf 必须为 1 或 3。");
        var battlefieldCount = bestOf == 3 ? 1 : BattlefieldCount;
        if (deck.Battlefields.Count != battlefieldCount)
            errors.Add($"现开{(bestOf == 3 ? "三局两胜" : "单局制")}必须提交 {battlefieldCount} 张战场牌。");

        // Share category checks, with constructed copy limits disabled. The
        // explicit hero-tag exception in 602.4.a.3.b applies to signature spells.
        ValidateMainDeck(cardsByNo, deck.MainDeck, null, errors, enforceConstructionLimits: false);
        foreach (var cardNo in deck.MainDeck.Distinct(StringComparer.Ordinal))
        {
            if (cardsByNo.TryGetValue(cardNo, out var card)
                && card.CardCategoryNameList.Any(category => category.StartsWith("专属", StringComparison.Ordinal) && category != "专属法术")
                && (legend is null || !string.Equals(card.Hero, legend.Hero, StringComparison.Ordinal)))
                errors.Add($"专属牌 {cardNo} 与所选传奇英雄标签不匹配（非专属法术仍遵守核心 103.3.d.2）。");
        }
        ValidateBattlefields(cardsByNo, deck.Battlefields, null, errors, allowDuplicates: true);
        foreach (var cardNo in deck.RuneDeck)
        {
            var rune = RequireCard(cardsByNo, cardNo, "runeDeck", errors);
            if (rune is not null && !SealedDeckRules.IsBasicRune(rune))
                errors.Add($"符文牌堆中的 {cardNo} 必须是六种基础符文之一。");
        }

        var usedCardNos = deck.MainDeck.Concat(deck.RuneDeck).Concat(deck.Battlefields)
            .Concat(deck.LegendCardNo.Length == 0 ? [] : new[] { deck.LegendCardNo });
        var usedCards = usedCardNos.Where(cardsByNo.ContainsKey).Select(cardNo => cardsByNo[cardNo]).ToArray();
        var traits = usedCards.SelectMany(card => card.CardColorList).Select(RuneTrait.Normalize)
            .Where(trait => trait.Length > 0 && trait != "colorless").Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (traits.Length > 3)
            errors.Add($"现开卡组实际使用的特性（含传奇、符文和战场）不能超过三种：{string.Join("、", traits.Select(TraitLabel))}。");

        if (context?.Pool is null)
            errors.Add("现开卡组校验缺少服务端卡池，不能提交。");
        else if (context.Pool.Any(entry => string.IsNullOrWhiteSpace(entry.Key) || entry.Value < 0))
            errors.Add("服务端现开卡池数据无效，不能提交。");
        else
        {
            // Champion is already part of MainDeck. Count the separate legend
            // once; never count the chosen champion a second time (core 103.3).
            foreach (var group in usedCards.Where(card => card.CardNo != SealedDeckRules.BlankBattlefieldCardNo
                && !SealedDeckRules.IsBasicRune(card)).GroupBy(card => card.CardNo, StringComparer.Ordinal))
            {
                var owned = context.Pool.GetValueOrDefault(group.Key);
                if (group.Count() > owned)
                    errors.Add($"卡牌 {group.Key} 使用 {group.Count()} 张，超过服务端卡池持有的 {owned} 张。");
            }
        }
        return errors.Count == 0 ? OfficialDeckValidationResult.Valid
            : new(false, errors.Distinct(StringComparer.Ordinal).ToArray());
    }
}
