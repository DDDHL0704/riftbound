using Riftbound.CardCatalog;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class UnitTokenLocationParserTests
{
    [Theory]
    [InlineData("SFD·031/221", "黄沙士兵", true)]
    [InlineData("OGN·265/298", "随从", true)]
    [InlineData("UNL-189/219", "精灵", true)]
    [InlineData("SFD·197/221", "黄沙士兵", false)]
    [InlineData("UNL-082/219", "精灵", false)]
    [InlineData("UNL-082a/219", "精灵", false)]
    [InlineData("OGN·222/298", "随从", false)]
    public async Task OfficialInstructionsKeepTheirExplicitLocationRestrictions(string no, string family, bool allowsChoice)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, card => card.CardNo == no);
        Assert.Equal(allowsChoice, UnitTokenLocationParser.AllowsOrdinaryLocationChoice(card.CardEffect, family));
    }

    [Theory]
    [InlineData(null, "精灵")]
    [InlineData("打出一名精灵。", null)]
    [InlineData("打出一名精灵。", "随从")]
    [InlineData("在相同位置打出一名精灵。", "精灵")]
    [InlineData("在此处打出一名精灵。", "精灵")]
    [InlineData("打出一名精灵。然后打出另一名精灵。", "精灵")]
    public void MissingOrAmbiguousInstructionsCannotInventAFreeLocation(string? text, string? family)
        => Assert.False(UnitTokenLocationParser.AllowsOrdinaryLocationChoice(text, family));
}
