using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ChinaStandardLegalityTests
{
    [Theory]
    [InlineData("预判攻势")]
    [InlineData("战或逃")]
    [InlineData("废料堆")]
    [InlineData("劫掠船巷")]
    [InlineData("幻梦之树")]
    [InlineData("力量方尖碑")]
    [InlineData("隐秘追踪者")]
    [InlineData("荣耀竞技场")]
    [InlineData("攀圣长阶")]
    public async Task EveryPrintingOfBannedNameIsRejected(string name)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var cards = catalog.Cards.Where(card => card.CardName == name).ToArray();
        Assert.NotEmpty(cards);
        Assert.All(cards, card => Assert.True(ChinaStandardBanList.IsBanned(card)));
    }

    [Fact]
    public async Task OnlyBloodDrenchedDravenIsBannedAndTwoVsTwoLegendBanIsNotImported()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var dravens = catalog.Cards.Where(card => card.CardName == "德莱文").ToArray();
        Assert.Contains(dravens, card => card.SubTitle == "血斧飞旋");
        Assert.Contains(dravens, card => card.SubTitle != "血斧飞旋");
        Assert.All(dravens, card => Assert.Equal(card.SubTitle == "血斧飞旋", ChinaStandardBanList.IsBanned(card)));
        var yi = catalog.Cards.Where(card => card.CardCategoryName == "传奇" && card.Hero == "易").ToArray();
        Assert.NotEmpty(yi);
        Assert.All(yi, card => Assert.False(ChinaStandardBanList.IsBanned(card)));
    }

    [Fact]
    public async Task AllCurrentPreconstructedDecksPassChinaStandardValidation()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var decks = PreconstructedDeckCatalog.Build(catalog, OfficialDeckFormat.ChinaStandard20260724);
        Assert.NotEmpty(decks);
        Assert.All(decks, deck => Assert.True(
            OfficialDeckValidator.Validate(deck.Decklist, catalog, OfficialDeckFormat.ChinaStandard20260724).IsValid));
    }

    [Fact]
    public async Task ServerRejectsBannedBattlefieldWithoutMutatingSession()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(CancellationToken.None);
        var deck = PreconstructedDeckCatalog.Build(catalog, OfficialDeckFormat.ChinaStandard20260724)[0].Decklist;
        var banned = catalog.Cards.First(card => card.CardName == "荣耀竞技场");
        deck = deck with { Battlefields = deck.Battlefields.Skip(1).Append(banned.CardNo).ToArray() };
        Assert.True(OfficialDeckValidator.Validate(deck, catalog).IsValid);
        var session = new MatchSession("CN-BAN", new CoreRuleEngine(), NoopMatchJournal.Instance,
            NoopMatchPlayerStore.Instance, new MatchSessionOptions(false, OfficialDeckFormat.ChinaStandard20260724));
        await session.EnsurePlayerAsync("p1", CancellationToken.None);
        var before = session.SnapshotFor("p1");
        var command = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo,
            deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        var result = await session.SubmitDeckAsync("p1", "banned-deck", command,
            JsonSerializer.SerializeToElement(command), CancellationToken.None);
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidDeck, result.ErrorCode);
        Assert.Contains("荣耀竞技场", result.ErrorMessage);
        Assert.Equal(before.Tick, session.SnapshotFor("p1").Tick);
        Assert.Empty(result.State.PlayerDecklists);
    }
}
