using Riftbound.CardCatalog;

namespace Riftbound.Engine;

/// <summary>
/// A named, server-validated preconstructed decklist players can submit without
/// hand-building a deck. Each deck is built from the official catalog and keeps
/// its explicit representative cards limited to implemented play behavior.
/// </summary>
public sealed record PreconstructedDeck(
    string Id,
    string Name,
    string Description,
    OfficialDecklist Decklist);

/// <summary>
/// Builds the shippable set of preconstructed decks. The card lists are derived from
/// the official catalog (not hard-coded card numbers) and each result is checked with
/// <see cref="OfficialDeckValidator"/> so an illegal definition fails fast rather than
/// reaching a player.
/// </summary>
public static class PreconstructedDeckCatalog
{
    private const int MaxManaCostForLowCurveUnit = 2;

    private static readonly IReadOnlyList<PreconstructedDeckDefinition> Definitions =
    [
        new("jhin-lowcurve", "影焰枪手 · 烬", "低费单位起手，正面稳健铺场。", "UNL-181/219", "UNL-022/219"),
        new("rumble-lowcurve", "机械狂潮 · 兰博", "机械单位压制，节奏明快。", "SFD·181/221", "SFD·026/221"),
        new("lillia-lowcurve", "梦境编织 · 莉莉娅", "灵动单位群进，灵活应对。", "UNL-189/219", "UNL-082/219"),
        new(
            "rumble-armaments",
            "锻炉武装 · 兰博",
            "加入百炼单位与武装装备，覆盖服务端装备结算路径。",
            "SFD·181/221",
            "SFD·026/221",
            ["SFD·085/221", "SFD·008/221", "SFD·022/221"]),
        new(
            "vex-spells",
            "暗影法术 · 薇古丝",
            "加入迅捷与高费法术代表，覆盖服务端法术栈结算路径。",
            "UNL-232/219",
            "UNL-055/219",
            ["OGN·183/298", "OGN·180/298"]),
        new(
            "vex-battlefields",
            "失落战场 · 薇古丝",
            "加入失落书库、崔法利兵营与疾风山丘，覆盖服务端战场规则路径。",
            "UNL-232/219",
            "UNL-055/219",
            ["OGN·183/298", "OGN·180/298"],
            ["UNL-211/219", "OGN·294/298", "OGN·297/298"]),
        new(
            "vex-response",
            "暗影响应 · 薇古丝",
            "加入黑影与提莫代表，覆盖服务端战斗响应与待命反应路径。",
            "UNL-232/219",
            "UNL-055/219",
            [
                "UNL-194/219",
                "UNL-194/219",
                "UNL-194/219",
                "OGN·197/298",
                "OGN·197/298",
                "OGN·197/298"
            ]),
        new(
            "poppy-standby",
            "班德尔待命 · 波比",
            "加入待命单位与班德尔树，覆盖服务端待命布置路径。",
            "UNL-203/219",
            "UNL-116/219",
            ["OGN·135/298"],
            ["OGN·278/298"]),
        new(
            "poppy-demacia",
            "德玛西亚阵线 · 波比",
            "加入盖伦、德玛西亚使节与强化阵地，覆盖服务端静态光环和防守坚守路径。",
            "UNL-203/219",
            "UNL-116/219",
            ["OGS·013/024", "UNL-092/219"],
            ["OGN·279/298"])
    ];

    public static IReadOnlyList<PreconstructedDeck> Build(OfficialCardCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var cardsByNo = catalog.Cards
            .Where(card => !string.IsNullOrWhiteSpace(card.CardNo))
            .ToDictionary(card => card.CardNo, StringComparer.Ordinal);

        var decks = new List<PreconstructedDeck>(Definitions.Count);
        foreach (var definition in Definitions)
        {
            var decklist = BuildLowCurveDeck(
                catalog,
                cardsByNo,
                definition.LegendCardNo,
                definition.ChampionCardNo,
                definition.RequiredMainDeckCardNos ?? [],
                definition.RequiredBattlefieldCardNos ?? []);
            var validation = OfficialDeckValidator.Validate(decklist, catalog);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(
                    $"Preconstructed deck '{definition.Id}' is not legal: {string.Join("; ", validation.Errors)}");
            }

            decks.Add(new PreconstructedDeck(definition.Id, definition.Name, definition.Description, decklist));
        }

        return decks;
    }

    private static OfficialDecklist BuildLowCurveDeck(
        OfficialCardCatalog catalog,
        IReadOnlyDictionary<string, OfficialCard> cardsByNo,
        string legendCardNo,
        string championCardNo,
        IReadOnlyList<string> requiredMainDeckCardNos,
        IReadOnlyList<string> requiredBattlefieldCardNos)
    {
        var legend = cardsByNo[legendCardNo];
        var allowedColors = legend.CardColorList.ToHashSet(StringComparer.Ordinal);
        var champion = cardsByNo[championCardNo];

        var implementedLowCurveUnits = CardBehaviorRegistry.GetAll()
            .Where(behavior => behavior.PlaysSourceToBaseAsUnit)
            .Where(behavior => behavior.RequiredTargetCount == 0 && behavior.MinTargetCount <= 0)
            .Where(behavior => string.IsNullOrWhiteSpace(behavior.Mode))
            .Where(behavior => behavior.ManaCost <= MaxManaCostForLowCurveUnit)
            .Select(behavior => behavior.CardNo)
            .Distinct(StringComparer.Ordinal)
            .Where(cardsByNo.ContainsKey)
            .Select(cardNo => cardsByNo[cardNo])
            .Where(card => IsMainDeckCandidate(card, allowedColors))
            .OrderBy(card => card.Energy ?? 0)
            .ThenBy(card => card.CardNo, StringComparer.Ordinal)
            .ToArray();

        var mainDeck = new List<string> { championCardNo };
        var nameCounts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [champion.CardName] = 1
        };
        foreach (var cardNo in requiredMainDeckCardNos)
        {
            if (!cardsByNo.TryGetValue(cardNo, out var requiredCard))
            {
                throw new InvalidOperationException($"Required preconstructed card '{cardNo}' was not found.");
            }

            if (!IsRequiredMainDeckCandidate(requiredCard, legend, allowedColors))
            {
                throw new InvalidOperationException(
                    $"Required preconstructed card '{cardNo}' is not legal for legend '{legendCardNo}'.");
            }

            mainDeck.Add(cardNo);
            nameCounts[requiredCard.CardName] = nameCounts.TryGetValue(requiredCard.CardName, out var current)
                ? current + 1
                : 1;
        }

        foreach (var card in implementedLowCurveUnits)
        {
            while (mainDeck.Count < OfficialDeckValidator.MinimumMainDeckCount
                && (!nameCounts.TryGetValue(card.CardName, out var count)
                    || count < OfficialDeckValidator.DefaultMaxCopiesByName))
            {
                mainDeck.Add(card.CardNo);
                nameCounts[card.CardName] = nameCounts.TryGetValue(card.CardName, out var current) ? current + 1 : 1;
            }

            if (mainDeck.Count >= OfficialDeckValidator.MinimumMainDeckCount)
            {
                break;
            }
        }

        if (mainDeck.Count < OfficialDeckValidator.MinimumMainDeckCount)
        {
            throw new InvalidOperationException(
                $"Unable to fill a legal {OfficialDeckValidator.MinimumMainDeckCount}-card main deck for legend {legendCardNo}.");
        }

        var runeDeck = catalog.Cards
            .Where(card => string.Equals(card.CardCategoryName, "符文", StringComparison.Ordinal))
            .Where(card => TraitsAllowed(card, allowedColors))
            .OrderBy(card => card.CardNo, StringComparer.Ordinal)
            .Select(card => card.CardNo)
            .Take(OfficialDeckValidator.RuneDeckCount)
            .ToArray();

        var battlefields = BuildBattlefields(catalog, cardsByNo, requiredBattlefieldCardNos);

        return new OfficialDecklist(legendCardNo, championCardNo, mainDeck, runeDeck, battlefields);
    }

    private static string[] BuildBattlefields(
        OfficialCardCatalog catalog,
        IReadOnlyDictionary<string, OfficialCard> cardsByNo,
        IReadOnlyList<string> requiredBattlefieldCardNos)
    {
        var selected = new List<OfficialCard>(OfficialDeckValidator.BattlefieldCount);
        var selectedNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var cardNo in requiredBattlefieldCardNos)
        {
            if (!cardsByNo.TryGetValue(cardNo, out var requiredCard))
            {
                throw new InvalidOperationException($"Required preconstructed battlefield '{cardNo}' was not found.");
            }

            if (!string.Equals(requiredCard.CardCategoryName, "战场", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Required preconstructed battlefield '{cardNo}' is not a battlefield.");
            }

            if (!selectedNames.Add(requiredCard.CardName))
            {
                throw new InvalidOperationException(
                    $"Required preconstructed battlefield '{cardNo}' duplicates battlefield name '{requiredCard.CardName}'.");
            }

            selected.Add(requiredCard);
        }

        foreach (var card in catalog.Cards
            .Where(card => string.Equals(card.CardCategoryName, "战场", StringComparison.Ordinal))
            .GroupBy(card => card.CardName, StringComparer.Ordinal)
            .Select(group => group.OrderBy(card => card.CardNo, StringComparer.Ordinal).First())
            .OrderBy(card => card.CardNo, StringComparer.Ordinal))
        {
            if (selected.Count >= OfficialDeckValidator.BattlefieldCount)
            {
                break;
            }

            if (!selectedNames.Add(card.CardName))
            {
                continue;
            }

            selected.Add(card);
        }

        if (selected.Count < OfficialDeckValidator.BattlefieldCount)
        {
            throw new InvalidOperationException(
                $"Unable to fill a legal {OfficialDeckValidator.BattlefieldCount}-card battlefield deck.");
        }

        return selected
            .Take(OfficialDeckValidator.BattlefieldCount)
            .Select(card => card.CardNo)
            .ToArray();
    }

    private static bool IsMainDeckCandidate(OfficialCard card, HashSet<string> allowedColors)
    {
        return card.CardCategoryName is "单位" or "英雄单位"
            && !card.CardCategoryName.StartsWith("专属", StringComparison.Ordinal)
            && card.CardGroupLimit != 1
            && !card.CardEffect.Contains("{{唯我}}", StringComparison.Ordinal)
            && !card.CardEffect.Contains("{{急速}}", StringComparison.Ordinal)
            && TraitsAllowed(card, allowedColors);
    }

    private static bool IsRequiredMainDeckCandidate(
        OfficialCard card,
        OfficialCard legend,
        HashSet<string> allowedColors)
    {
        return !string.IsNullOrWhiteSpace(card.CardNo)
            && IsRequiredMainDeckCategoryAllowed(card, legend)
            && card.CardGroupLimit != 1
            && !card.CardEffect.Contains("{{唯我}}", StringComparison.Ordinal)
            && TraitsAllowed(card, allowedColors)
            && CardBehaviorRegistry.TryGetByCardNo(card.CardNo, out var behavior)
            && IsImplementedMainDeckPlayBehavior(card, behavior);
    }

    private static bool IsRequiredMainDeckCategoryAllowed(OfficialCard card, OfficialCard legend)
    {
        if (!card.CardCategoryName.StartsWith("专属", StringComparison.Ordinal))
        {
            return card.CardCategoryName is "单位" or "英雄单位" or "装备" or "法术";
        }

        return card.CardCategoryName is "专属单位" or "专属装备" or "专属法术"
            && !string.IsNullOrWhiteSpace(card.Hero)
            && string.Equals(card.Hero, legend.Hero, StringComparison.Ordinal);
    }

    private static bool IsImplementedMainDeckPlayBehavior(OfficialCard card, CardBehaviorDefinition behavior)
    {
        if (card.CardCategoryName.Contains("单位", StringComparison.Ordinal))
        {
            return behavior.PlaysSourceToBaseAsUnit;
        }

        if (card.CardCategoryName.Contains("装备", StringComparison.Ordinal))
        {
            return behavior.PlaysSourceToBaseAsEquipment;
        }

        return card.CardCategoryName.Contains("法术", StringComparison.Ordinal)
            && !behavior.PlaysSourceToBaseAsUnit
            && !behavior.PlaysSourceToBaseAsEquipment;
    }

    private static bool TraitsAllowed(OfficialCard card, HashSet<string> allowedColors)
    {
        return card.CardColorList.All(color => string.Equals(color, "colorless", StringComparison.Ordinal)
            || allowedColors.Contains(color));
    }

    private sealed record PreconstructedDeckDefinition(
        string Id,
        string Name,
        string Description,
        string LegendCardNo,
        string ChampionCardNo,
        IReadOnlyList<string>? RequiredMainDeckCardNos = null,
        IReadOnlyList<string>? RequiredBattlefieldCardNos = null);
}
