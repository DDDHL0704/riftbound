using Riftbound.CardCatalog;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class UnlTokenBattlefieldAuditTests
{
    [Fact]
    public async Task AConstructedDeckCannotStartWithABaronNestBattlefieldToken()
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        Assert.Single(catalog.Cards, card => card.CardNo == "UNL·T01");
        var deck = PreconstructedDeckCatalog.Build(catalog).First().Decklist;
        var ordinary = OfficialDeckValidator.Validate(deck, catalog);
        Assert.True(ordinary.IsValid, string.Join("; ", ordinary.Errors));
        var fields = deck.Battlefields.ToArray();
        fields[0] = "UNL·T01";
        var rejected = OfficialDeckValidator.Validate(deck with { Battlefields = fields }, catalog);
        Assert.False(rejected.IsValid);
        Assert.Contains(rejected.Errors, error => error.Contains("UNL·T01") && error.Contains("必须是战场牌"));
    }
}
