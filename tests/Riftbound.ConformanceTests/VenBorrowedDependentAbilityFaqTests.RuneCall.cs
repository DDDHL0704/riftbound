using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(2, false)]
    [InlineData(0, true)]
    [InlineData(2, true)]
    public async Task BorrowedDrawAndRuneCallResolveAfterResponsesEvenWithAnEmptyRuneDeck(int runes, bool returns)
    {
        var initial = RadGearTestPosition.Put(RadGearTestPosition.Create(), "HOST", "OGN·111/298", "P1", "BASE");
        initial = RadGearTestPosition.Put(initial, "DONOR", "RAD·046/167", "P1", "BASE");
        initial = RadGearTestPosition.Put(initial, "RETURN", "OGN·104/298", "P1", "HAND");
        for (var i = 0; i < runes; i++)
            initial = RadGearTestPosition.Put(initial, "BORROWED-RUNE-" + i, "UNL-R03", "P1", "RUNE_DECK");
        var game = new VenBasicFlowSpellTests.Driver(initial);
        var id = TemplateId(initial, "召出");
        await RejectBorrowedResource(initial, "HOST", id, ["ALLY"]);
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, []));
        Assert.Equal(initial.PlayerZones["P1"].MainDeck, game.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(initial.PlayerZones["P1"].RuneDeck, game.State.PlayerZones["P1"].RuneDeck);
        Assert.False(game.State.CardObjects["DONOR"].IsExhausted);
        if (returns)
        {
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
            await game.Top();
        }
        await game.Top();
        Assert.Equal(initial.PlayerZones["P1"].MainDeck.Skip(1), game.State.PlayerZones["P1"].MainDeck);
        Assert.Contains(initial.PlayerZones["P1"].MainDeck[0], game.State.PlayerZones["P1"].Hand);
        Assert.Equal(initial.PlayerZones["P1"].RuneDeck.Skip(1), game.State.PlayerZones["P1"].RuneDeck);
        if (runes > 0)
        {
            Assert.Contains("BORROWED-RUNE-0", game.State.PlayerZones["P1"].Base);
            Assert.True(game.State.CardObjects["BORROWED-RUNE-0"].IsExhausted);
        }
        Assert.Equal(initial.CardObjects["ALLY"].Power, game.State.CardObjects["ALLY"].Power);
        await game.Replay();
    }
}
