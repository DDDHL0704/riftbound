using System.Reflection;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OfficialMultiTypeIdentityTests
{
    [Fact]
    public async Task ActualUnitGearRecordCreatesBothTypesWithoutPromotingItsBehavior()
    {
        var card = Assert.Single((await Source()).Cards);
        Assert.Equal(37943, card.Id);
        Assert.Equal("VEN·058", card.CardNo);
        Assert.Equal(new[] { "单位", "装备" }, card.CardCategoryNameList);
        var created = Create(card);
        Assert.Contains(CardObjectTags.UnitCard, created.Tags);
        Assert.Contains(CardObjectTags.EquipmentCard, created.Tags);
        Assert.Equal(2, created.Power);
        Assert.Equal("P1", created.OwnerId);
        Assert.Equal("P1", created.ControllerId);
        Assert.Contains("机械", created.Tags);
        Assert.Contains("魄罗", created.Tags);
    }

    [Fact]
    public async Task ActualUnitGearIsLegalInAMatchingLegendMainDeck()
    {
        var (catalog, deck, _) = await Deck();
        var validation = OfficialDeckValidator.Validate(deck, catalog);
        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourceCategoryOrderDoesNotDropEitherType(bool reverse)
    {
        var card = Assert.Single((await Source()).Cards);
        if (reverse) card = card with { CardCategoryName = "装备 / 单位",
            CardCategoryNameList = ["装备", "单位"], CardCategoryList = ["equipment", "unit"] };
        var types = Create(card).Tags.Where(t => t.StartsWith("CARD_TYPE:", StringComparison.Ordinal)).Order().ToArray();
        Assert.Equal(new[] { CardObjectTags.EquipmentCard, CardObjectTags.UnitCard }, types);
    }

    [Theory]
    [InlineData("符文")]
    [InlineData("战场")]
    [InlineData("传奇")]
    [InlineData("指示物单位")]
    [InlineData("未知")]
    public async Task AUnitTypeCannotHideAnIllegalAdditionalMainDeckCategory(string forbidden)
    {
        var (catalog, deck, card) = await Deck();
        var forged = card with { CardCategoryName = "单位 / " + forbidden,
            CardCategoryNameList = ["单位", forbidden] };
        var validation = OfficialDeckValidator.Validate(deck, catalog with {
            Cards = catalog.Cards.Select(c => c.CardNo == card.CardNo ? forged : c).ToArray() });
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, e => e.Contains("不能使用类别", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExclusiveCategoryInTheSecondPositionStillRequiresTheLegendHero()
    {
        var (catalog, deck, card) = await Deck();
        var forged = card with { CardCategoryName = "单位 / 专属装备",
            CardCategoryNameList = ["单位", "专属装备"], Hero = "另一个英雄" };
        var validation = OfficialDeckValidator.Validate(deck, catalog with {
            Cards = catalog.Cards.Select(c => c.CardNo == card.CardNo ? forged : c).ToArray() });
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, e => e.Contains("与所选传奇英雄标签不匹配", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("OGN·096/298", CardObjectTags.UnitCard)]
    [InlineData("SFD·059/221", CardObjectTags.EquipmentCard)]
    [InlineData("OGN·048/298", CardObjectTags.SpellCard)]
    [InlineData("OGN·007/298", CardObjectTags.RuneCard)]
    [InlineData("UNL-181/219", "CARD_TYPE:LEGEND")]
    [InlineData("OGN·296/298", "CARD_TYPE:BATTLEFIELD")]
    public async Task ExistingSingleTypePrintingsKeepTheirExactType(string no, string expected)
    {
        var card = (await OfficialCardCatalog.LoadDefaultAsync()).Cards.Single(c => c.CardNo == no);
        Assert.Equal(expected, Assert.Single(Create(card).Tags, t => t.StartsWith("CARD_TYPE:", StringComparison.Ordinal)));
    }

    internal static async Task<(OfficialCardCatalog Catalog, OfficialDecklist Deck, OfficialCard Card)> Deck()
    {
        var source = Assert.Single((await Source()).Cards);
        var current = await OfficialCardCatalog.LoadDefaultAsync();
        var deck = PreconstructedDeckCatalog.Build(current).Single(d => d.Id == "jhin-lowcurve").Decklist;
        var main = deck.MainDeck.ToArray();
        main[Array.FindIndex(main, n => n != deck.ChampionCardNo)] = source.CardNo;
        var cards = current.Cards.Where(c => c.CardNo != source.CardNo).Append(source).ToArray();
        return (current with { Cards = cards, Total = cards.Length },
            deck with { MainDeck = main }, source);
    }

    internal static CardObjectState Create(OfficialCard card)
    {
        var factory = typeof(CoreRuleEngine).Assembly.GetType("Riftbound.Engine.PrintedCardFactory")!;
        return Assert.IsType<CardObjectState>(factory.GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, ["UNIT-GEAR", "P1", card]));
    }

    internal static async Task<OfficialCardCatalog> Source()
    {
        return await OfficialCardCatalogLoader.LoadFromFileAsync(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "official-source", "multitype", "catalog-fixture.json"));
    }
}
