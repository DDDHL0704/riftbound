using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class NamiHeldNextUnitTests
{
    [Fact]
    public async Task FourActuallyPlayedWarhawksConsumeOnlyTheFirstActualTokenPlay()
    {
        var initial = RadGearTestPosition.Put(Position(), "BIRDS", "UNL-044/219", "P1", "HAND");
        var game = new Driver(initial);
        await EarnHold(game); await game.Top(); await Fund(game, blue: true);
        await game.Act("P1", new RecycleRuneCommand("FUNDS-1"));
        var prefix = game.Events().Length;
        await game.Act("P1", new PlayCardCommand("BIRDS", "UNL-044/219", [], Mode: "CREATE_WARHAWKS"));
        await game.Top();
        var played = game.Events().Skip(prefix).Where(e => e.Event.Kind == "UNIT_TOKEN_CREATED")
            .Select(e => Assert.IsType<string>(e.Event.Payload["tokenObjectId"])).ToArray();
        Assert.Equal(4, played.Length);
        Assert.All(played, id => {
            Assert.Equal(P6TokenFactoryCatalog.WarhawkTokenCardNo, game.State.CardObjects[id].TokenFactoryCardNo);
            Assert.True(game.State.CardObjects[id].IsExhausted);
            Assert.Equal(0, BoonRules.Count(game.State.CardObjects[id]));
        });
        var skill = Assert.Single(game.State.StackItems);
        Assert.Equal(played[0], skill.TurnUnitPlayBoon!.Unit.ObjectId);
        await game.ColdCheckpoint();
        await game.Top();
        Assert.False(game.State.CardObjects[played[0]].IsExhausted);
        Assert.Equal(1, BoonRules.Count(game.State.CardObjects[played[0]]));
        Assert.Equal(2, game.State.CardObjects[played[0]].Power);
        Assert.All(played.Skip(1), id => {
            Assert.True(game.State.CardObjects[id].IsExhausted);
            Assert.Equal(0, BoonRules.Count(game.State.CardObjects[id]));
            Assert.Equal(1, game.State.CardObjects[id].Power);
        });
        Assert.Empty(game.State.StackItems);
        await game.ColdCheckpoint();
    }
}
