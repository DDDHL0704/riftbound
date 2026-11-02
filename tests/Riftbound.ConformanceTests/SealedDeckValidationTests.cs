using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SealedDeckValidationTests
{
    private const string Common = "OGN·001/298";
    private const string Champion = "OGN·030/298";
    private const string Legend = "OGN·253/298"; // Darius; deliberately different from Jinx.
    private const string Rune = "SFD·R01"; // May come from outside the opened series/pool.
    private const string Blank = SealedDeckRules.BlankBattlefieldCardNo;
    private static Task<OfficialCardCatalog> Catalog() => OfficialCardCatalog.LoadRuntimeAsync();
    private static OfficialDecklist Deck(int count = 25) => new("", "",
        Enumerable.Repeat(Common, count).ToArray(), Enumerable.Repeat(Rune, 12).ToArray(), [Blank, Blank, Blank]);
    private static Dictionary<string, int> Pool(OfficialDecklist deck) => deck.MainDeck.Concat(deck.Battlefields)
        .Append(deck.LegendCardNo).Where(no => !string.IsNullOrWhiteSpace(no) && no != Blank)
        .GroupBy(no => no, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
    private static OfficialDeckValidationResult Validate(OfficialCardCatalog catalog, OfficialDecklist deck,
        IReadOnlyDictionary<string, int>? pool = null, int bestOf = 1) =>
        OfficialDeckValidator.Validate(deck, catalog, OfficialDeckFormat.Sealed, new(pool ?? Pool(deck), bestOf));
    private static void Valid(OfficialDeckValidationResult result) => Assert.True(result.IsValid, string.Join("；", result.Errors));
    private static void Invalid(OfficialDeckValidationResult result, string message)
    {
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains(message, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task LegendAndChampionAreOptionalAndTheirHeroTagsNeedNotMatch(bool legend, bool champion)
    {
        var deck = Deck() with { LegendCardNo = legend ? Legend : "", ChampionCardNo = champion ? Champion : "" };
        if (champion) deck = deck with { MainDeck = deck.MainDeck.Skip(1).Append(Champion).ToArray() };
        var pool = Pool(deck);
        if (champion) Assert.Equal(1, pool[Champion]);
        Valid(Validate(await Catalog(), deck, pool));
    }

    [Fact]
    public async Task NullLegendAndChampionNormalizeToOmittedAndWireCommandRemainsSubmitDeck()
    {
        var command = Assert.IsType<SubmitDeckCommand>(GameCommandJsonMapper.Map(JsonSerializer.SerializeToElement(new {
            cmdType = "SUBMIT_DECK", legendCardNo = (string?)null, championCardNo = (string?)null,
            sealedPool = new { pool = new[] { new { cardNo = Common, count = 999 } } },
            mainDeck = Deck().MainDeck, runeDeck = Deck().RuneDeck, battlefields = Deck().Battlefields
        })));
        var deck = new OfficialDecklist(command.LegendCardNo, command.ChampionCardNo, command.MainDeck, command.RuneDeck, command.Battlefields);
        Valid(Validate(await Catalog(), deck));
        Invalid(OfficialDeckValidator.Validate(deck, await Catalog(), OfficialDeckFormat.Sealed), "缺少服务端卡池");
        Valid(Validate(await Catalog(), Deck() with { LegendCardNo = null!, ChampionCardNo = null! }));
    }

    [Theory]
    [InlineData(24, false)]
    [InlineData(25, true)]
    [InlineData(40, true)]
    [InlineData(52, true)]
    public async Task MainDeckHasMinimumTwentyFiveAndNoMaximum(int count, bool legal)
    {
        var result = Validate(await Catalog(), Deck(count));
        if (legal) Valid(result); else Invalid(result, "至少需要 25");
    }

    [Fact]
    public async Task ChosenChampionCountsInMainDeckAndInPoolExactlyOnce()
    {
        var catalog = await Catalog();
        var deck = Deck() with { ChampionCardNo = Champion, MainDeck = Deck(24).MainDeck.Append(Champion).ToArray() };
        Valid(Validate(catalog, deck));
        Invalid(Validate(catalog, deck with { MainDeck = Deck(23).MainDeck.Append(Champion).ToArray() }), "至少需要 25");
        Invalid(Validate(catalog, deck with { MainDeck = Deck().MainDeck }), "必须包含 1 张所选英雄");
        var pool = Pool(deck); pool[Champion] = 0;
        Invalid(Validate(catalog, deck, pool), Champion);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(13)]
    public async Task RuneDeckMustContainExactlyTwelve(int count) =>
        Invalid(Validate(await Catalog(), Deck() with { RuneDeck = Enumerable.Repeat(Rune, count).ToArray() }), "正好包含 12");

    [Fact]
    public async Task BasicRunesFromOtherSeriesAndAlternatePrintsAreUnlimited()
    {
        var catalog = await Catalog();
        var deck = Deck() with { RuneDeck = Enumerable.Repeat("OGN·007a/298", 6).Concat(Enumerable.Repeat(Rune, 6)).ToArray() };
        var pool = Pool(deck);
        Assert.DoesNotContain(Rune, pool.Keys);
        Assert.DoesNotContain("OGN·007a/298", pool.Keys);
        Valid(Validate(catalog, deck, pool));
        Assert.DoesNotContain(Blank, pool.Keys);
    }

    [Theory]
    [InlineData("VEN·005")]
    [InlineData(Common)]
    public async Task NonbasicRunesAndNonRuneCardsAreRejectedEvenWhenOwned(string cardNo)
    {
        var deck = Deck() with { RuneDeck = Deck().RuneDeck.Skip(1).Append(cardNo).ToArray() };
        var pool = Pool(deck); pool[cardNo] = 26;
        Invalid(Validate(await Catalog(), deck, pool), "六种基础符文");
    }

    [Fact]
    public async Task ActualUnionIncludesEveryLegendRuneAndExclusiveSpellTrait()
    {
        var catalog = await Catalog();
        var ornn = catalog.Cards.First(card => card.CardCategoryName == "传奇" && card.Hero == "奥恩");
        var three = Deck() with { LegendCardNo = ornn.CardNo, MainDeck = Deck(24).MainDeck.Append("SFD·190/221").ToArray() }; // red + green/blue
        Valid(Validate(catalog, three));
        var four = three with { RuneDeck = Deck().RuneDeck.Skip(1).Append("OGN·126/298").ToArray() }; // orange
        Invalid(Validate(catalog, four), "不能超过三种");
        var legendFour = Deck() with { LegendCardNo = Legend, RuneDeck = Enumerable.Repeat("OGN·042/298", 6)
            .Concat(Enumerable.Repeat("OGN·089/298", 6)).ToArray() }; // legend red/yellow + green/blue
        Invalid(Validate(catalog, legendFour), "不能超过三种");
        var signature = catalog.Cards.First(card => card.CardCategoryName == "专属法术"
            && card.CardColorList.Count == 2 && card.CardColorList.All(color => color is "red" or "yellow"));
        var signatureDeck = Deck() with { MainDeck = Deck(24).MainDeck.Append(signature.CardNo).ToArray(),
            RuneDeck = Enumerable.Repeat("OGN·042/298", 6).Concat(Enumerable.Repeat("OGN·089/298", 6)).ToArray() };
        Invalid(Validate(catalog, signatureDeck), "不能超过三种");
    }

    [Fact]
    public async Task CopyUniqueAndExclusiveCountLimitsAreWaivedWithFullPoolOwnership()
    {
        var catalog = await Catalog();
        var ornn = catalog.Cards.First(card => card.CardCategoryName == "传奇" && card.Hero == "奥恩");
        foreach (var no in new[] { "SFD·190/221", "SFD·191/221", "SFD·192/221" })
            Valid(Validate(catalog, Deck() with { LegendCardNo = ornn.CardNo,
                MainDeck = Deck(20).MainDeck.Concat(Enumerable.Repeat(no, 5)).ToArray() }));
        var signature = catalog.Cards.First(card => card.CardCategoryName == "专属法术" && card.CardColorList.Contains("red"));
        Valid(Validate(catalog, Deck() with { MainDeck = Deck(20).MainDeck.Concat(Enumerable.Repeat(signature.CardNo, 5)).ToArray() }));
    }

    [Theory]
    [InlineData("SFD·190/221")]
    [InlineData("UNL-194/219")]
    public async Task NonSpellExclusiveCardsKeepTheCoreLegendTagRequirement(string cardNo)
    {
        var catalog = await Catalog();
        var exclusive = catalog.Cards.Single(card => card.CardNo == cardNo);
        var legend = catalog.Cards.First(card => card.CardCategoryName == "传奇" && card.Hero == exclusive.Hero);
        var deck = Deck() with { LegendCardNo = legend.CardNo, MainDeck = Deck(24).MainDeck.Append(cardNo).ToArray() };
        Valid(Validate(catalog, deck));
        Invalid(Validate(catalog, deck with { LegendCardNo = "" }), "非专属法术仍遵守核心");
        Invalid(Validate(catalog, deck with { LegendCardNo = Legend }), "非专属法术仍遵守核心");
    }

    [Fact]
    public async Task ConstructedBanListDoesNotApplyToSealed()
    {
        var catalog = await Catalog();
        var banned = catalog.Cards.First(card => card.CardName == "战或逃" && card.ExtendRarity == "base_inscript");
        var deck = Deck() with { MainDeck = Enumerable.Repeat(banned.CardNo, 25).ToArray() };
        Valid(Validate(catalog, deck));
        Assert.True(ChinaStandardBanList.IsAllowed(banned, OfficialDeckFormat.Sealed));
        Assert.False(ChinaStandardBanList.IsAllowed(banned, OfficialDeckFormat.ChinaStandard20260724));
        Invalid(OfficialDeckValidator.Validate(deck, catalog, OfficialDeckFormat.ChinaStandard20260724), "禁卡");
    }

    [Fact]
    public async Task PoolCountsActualPhysicalPrintsAndNeverSubstitutesAnAlternatePrinting()
    {
        var catalog = await Catalog();
        var deck = Deck() with { MainDeck = Deck(24).MainDeck.Append("OGN·027a/298").ToArray() };
        var pool = Pool(deck); pool.Remove("OGN·027a/298"); pool["OGN·027/298"] = 1;
        var before = JsonSerializer.Serialize(pool);
        Invalid(Validate(catalog, deck, pool), "OGN·027a/298");
        Assert.Equal(before, JsonSerializer.Serialize(pool));
        pool["OGN·027a/298"] = 1;
        Valid(Validate(catalog, deck, pool));
        pool[Common] = 23;
        Invalid(Validate(catalog, deck, pool), "使用 24 张");
    }

    [Fact]
    public async Task SeparateLegendConsumesPoolAndUntrustedMissingContextFailsClosed()
    {
        var catalog = await Catalog();
        var deck = Deck() with { LegendCardNo = Legend };
        Invalid(Validate(catalog, deck, new Dictionary<string, int> { [Common] = 25 }), Legend);
        Invalid(OfficialDeckValidator.Validate(deck, catalog, OfficialDeckFormat.Sealed), "缺少服务端卡池");
        var pool = Pool(deck); pool["invalid"] = -1;
        Invalid(Validate(catalog, deck, pool), "卡池数据无效");
    }

    [Fact]
    public void ExistingSubmissionEntryCannotAcceptASealedDeckWithoutAnAuthoritativePool()
    {
        // Sealed seats now always receive server-generated packs. The old incomplete
        // options must fail before seating instead of admitting a pool-less session.
        var error = Assert.Throws<ArgumentException>(() => new MatchSession("sealed-no-pool",
            new CoreRuleEngine(), NoopMatchJournal.Instance, NoopMatchPlayerStore.Instance,
            new MatchSessionOptions(false, OfficialDeckFormat.Sealed)));
        Assert.Equal("options", error.ParamName);
        Assert.Contains("隐藏提交内容", error.Message);
    }

    [Theory]
    [InlineData(1, 3)]
    [InlineData(3, 1)]
    public async Task BattlefieldsUseBestOfSpecificCountAndRepeatedBlankCardsAreUnlimited(int bestOf, int required)
    {
        var catalog = await Catalog();
        var deck = Deck() with { Battlefields = Enumerable.Repeat(Blank, required).ToArray() };
        Valid(Validate(catalog, deck, bestOf: bestOf));
        Invalid(Validate(catalog, deck with { Battlefields = Enumerable.Repeat(Blank, required + 1).ToArray() }, bestOf: bestOf), $"提交 {required} 张");
        Invalid(Validate(catalog, deck, bestOf: 2), "必须为 1 或 3");
    }

    [Fact]
    public async Task RepeatedRealBattlefieldsRequireTheSameNumberOfPhysicalCopies()
    {
        var catalog = await Catalog();
        var battlefield = catalog.Cards.First(card => card.CardCategoryName == "战场"
            && card.CardColorList.All(color => color is "red" or "colorless"));
        var deck = Deck() with { Battlefields = Enumerable.Repeat(battlefield.CardNo, 3).ToArray() };
        var pool = Pool(deck); pool[battlefield.CardNo] = 2;
        Invalid(Validate(catalog, deck, pool), "使用 3 张");
        pool[battlefield.CardNo] = 3;
        Valid(Validate(catalog, deck, pool));
    }

    [Fact]
    public async Task SharedCategoryChecksRejectTokensUnknownCardsAndWrongLegendOrChampionTypes()
    {
        var catalog = await Catalog();
        var token = catalog.Cards.First(OfficialRuleDomainBehaviorCatalog.IsTokenCard);
        Invalid(Validate(catalog, Deck() with { MainDeck = Deck(24).MainDeck.Append(token.CardNo).ToArray() }), "不能使用指示物");
        Invalid(Validate(catalog, Deck() with { MainDeck = Deck(24).MainDeck.Append("unknown").ToArray() }), "未知牌号");
        Invalid(Validate(catalog, Deck() with { LegendCardNo = Common }), "必须指向一张传奇");
        Invalid(Validate(catalog, Deck() with { ChampionCardNo = Common }), "必须指向一张英雄单位");
        Invalid(Validate(catalog, Deck() with { Battlefields = [Common, Blank, Blank] }), "必须是战场牌");
        Invalid(Validate(catalog, Deck() with { MainDeck = Deck(24).MainDeck.Append(Blank).ToArray() }), "不能使用类别");
    }

    [Fact]
    public async Task ConstructedStillRejectsBlankBattlefieldsAndMissingLegendHero()
    {
        var catalog = await Catalog();
        var result = OfficialDeckValidator.Validate(Deck(40), catalog);
        Invalid(result, "传奇牌编号");
        Invalid(result, "英雄牌编号");
        Invalid(result, "未知牌号 " + Blank);
    }
}
