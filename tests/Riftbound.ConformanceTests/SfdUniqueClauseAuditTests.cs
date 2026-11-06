using Riftbound.CardCatalog;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SfdUniqueClauseAuditTests
{
    [Theory]
    [InlineData("SFD·190/221")]
    [InlineData("SFD·191/221")]
    [InlineData("SFD·192/221")]
    public async Task ConstructedDeckAllowsOneCopyButRejectsTwoCopiesOfEachUniqueEquipment(string no)
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var card = Assert.Single(catalog.Cards, c => c.CardNo == no);
        var legend = catalog.Cards.First(c => c.CardCategoryName == "传奇" && c.Hero == card.Hero);
        bool InDomain(OfficialCard c) => c.CardColorList.All(color => color == "colorless" || legend.CardColorList.Contains(color));
        var champion = catalog.Cards.First(c => c.CardCategoryName == "英雄单位" && c.Hero == legend.Hero && InDomain(c));
        var main = new List<string> { champion.CardNo, no };
        var filler = catalog.Cards.Where(c => c.CardCategoryName is "单位" or "法术" or "装备")
            .Where(InDomain).Where(c => c.CardGroupLimit != 1 && c.CardName != card.CardName && c.CardName != champion.CardName)
            .GroupBy(c => c.CardName).Select(group => group.First()).OrderBy(c => c.CardNo, StringComparer.Ordinal);
        foreach (var c in filler)
        {
            main.AddRange(Enumerable.Repeat(c.CardNo, Math.Min(3, 40 - main.Count)));
            if (main.Count == 40) break;
        }
        Assert.Equal(40, main.Count);
        var rune = catalog.Cards.First(c => c.CardCategoryName == "符文" && InDomain(c));
        var fields = catalog.Cards.Where(c => c.CardCategoryName == "战场" && InDomain(c))
            .GroupBy(c => c.CardName).Select(group => group.First().CardNo).Take(3).ToArray();
        var deck = new OfficialDecklist(legend.CardNo, champion.CardNo, main,
            Enumerable.Repeat(rune.CardNo, 12).ToArray(), fields);
        var one = OfficialDeckValidator.Validate(deck, catalog);
        Assert.True(one.IsValid, string.Join("; ", one.Errors));
        var second = catalog.Cards.FirstOrDefault(c => c.CardName == card.CardName && c.CardNo != no)?.CardNo ?? no;
        main[^1] = second;
        var two = OfficialDeckValidator.Validate(deck, catalog);
        Assert.False(two.IsValid);
        var error = Assert.Single(two.Errors);
        Assert.Contains(card.CardName, error);
        Assert.Contains("上限为 1 张", error);
    }
}
