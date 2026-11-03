using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    // Core 337.2 / 338.1.a.4: drawing cards does not qualify for the
    // immediate resolution exception granted to resource abilities.
    [Theory]
    [InlineData("SFD·199/221")]
    [InlineData("SFD·248/221")]
    public async Task NativeExplorerReactionKeepsAnIndependentResponseWindowBeforeDrawing(string donor)
    {
        var initial = ExplorerReactionPosition(donor);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["UNIT"] = initial.CardObjects["UNIT"] with { IsExhausted = false } } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("EXPLORER-TARGET-1", "OGN·004/298", ["ENEMY"]));
        await game.Top();
        await CloseExplorerDuel(game);
        await game.Act("P1", new PlayCardCommand("EXPLORER-TARGET-2", "OGN·004/298", ["ENEMY"]));
        Assert.Contains("EZREAL_ENEMY_TARGETS_THIS_TURN:P1:2", game.State.UntilEndOfTurnEffects);
        var before = game.State;
        var secondSpell = Assert.Single(before.StackItems).StackItemId;
        await game.Act("P1", new LegendActCommand("UNIT", LegendActionAbilityCatalog.EzrealLegendAbilityId, [], []));
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(before.PlayerZones["P1"].MainDeck, game.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(before.PlayerZones["P1"].Hand, game.State.PlayerZones["P1"].Hand);
        Assert.Equal(2, game.State.StackItems.Count);
        await game.Top();
        Assert.Contains(before.PlayerZones["P1"].MainDeck[0], game.State.PlayerZones["P1"].Hand);
        Assert.Equal(secondSpell, Assert.Single(game.State.StackItems).StackItemId);
        await game.Top();
        await CloseExplorerDuel(game);
        await game.Replay();
    }
}
