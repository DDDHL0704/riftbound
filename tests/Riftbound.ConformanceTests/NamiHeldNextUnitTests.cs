using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class NamiHeldNextUnitTests
{
    private const string Nami = "UNL-052/219";
    private const string Unit = "OGN·096/298";

    private static MatchState Position()
    {
        var state = ExactOtherFriendlyHoldTests.Position(Nami, 0);
        state = RadGearTestPosition.Put(state, "HOST", Nami, "P1", "BATTLEFIELD", "F");
        state = RadGearTestPosition.Put(state, "FIRST", Unit, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "SECOND", Unit, "P1", "HAND");
        for (var i = 0; i < 8; i++)
            state = RadGearTestPosition.Put(state, "FUNDS-" + i, "UNL-R03", "P1", "BASE");
        return state;
    }

    private static async Task EarnHold(Driver game)
    {
        await game.Act("P2", new EndTurnCommand());
        Assert.Equal("P1", game.State.TurnPlayerId);
        Assert.Equal(1, game.State.PlayerScores["P1"]);
        Assert.Single(game.State.StackItems, item => item.UnitEvent?.CardNo == Nami);
    }

    private static async Task Fund(Driver game, bool blue = false)
    {
        await game.TapRunes("P1", game.State.CardObjects.Values.Where(c => c.ControllerId == "P1"
            && c.Tags.Contains(CardObjectTags.RuneCard) && !c.IsExhausted
            && game.State.PlayerZones["P1"].Base.Contains(c.ObjectId)).Select(c => c.ObjectId).Order(StringComparer.Ordinal));
        if (blue) await game.Act("P1", new RecycleRuneCommand("FUNDS-0"));
    }

    // CN391.2 / 392 / 393: the held skill creates a delayed trigger.
    // The next unit first enters normally; both players may respond before it readies and gains Boon.
    [Fact]
    public async Task AnActualHoldCreatesAnIndependentReadyAndBoonSkillForOnlyTheNextUnitPlay()
    {
        var game = new Driver(Position());
        await EarnHold(game);
        await game.Top();
        await Fund(game);
        await game.Act("P1", new PlayCardCommand("FIRST", Unit, []));
        Assert.True(game.State.CardObjects["FIRST"].IsExhausted);
        Assert.Equal(0, BoonRules.Count(game.State.CardObjects["FIRST"]));
        var delayed = Assert.Single(game.State.StackItems);
        Assert.NotNull(delayed.TurnUnitPlayBoon);
        Assert.Equal("FIRST", delayed.TurnUnitPlayBoon!.Unit.ObjectId);
        await game.ColdCheckpoint();
        await game.Top();
        Assert.False(game.State.CardObjects["FIRST"].IsExhausted);
        Assert.Equal(1, BoonRules.Count(game.State.CardObjects["FIRST"]));
        Assert.Equal(2, game.State.CardObjects["FIRST"].Power);
        await game.Act("P1", new PlayCardCommand("SECOND", Unit, []));
        Assert.True(game.State.CardObjects["SECOND"].IsExhausted);
        Assert.Equal(0, BoonRules.Count(game.State.CardObjects["SECOND"]));
        Assert.Empty(game.State.StackItems);
        await game.ColdCheckpoint();
    }
}
