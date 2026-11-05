using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class EncourageDistinctCardTests
{
    // CN 812.1.c: replaying the same physical card is not another card,
    // even though returning it to a hand changes its object generation.
    [Theory]
    [InlineData("OGN·012/298", 4)]
    [InlineData("OGN·016/298", 3)]
    [InlineData("OGN·020/298", 5)]
    [InlineData("OGN·217/298", 2)]
    [InlineData("OGN·218/298", 3)]
    public async Task OpponentReturningTheOnlyPlayedCardDoesNotEnableItsInspire(string no, int mana)
    {
        var initial = VenUnitEntryBasicTests.Position(no);
        initial = initial with {
            PlayerZones = initial.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P2"
                ? p.Value with { Base = [], Battlefields = ["OTHER", "ENEMY"] } : p.Value),
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["ENEMY"] = new("P2", "BATTLEFIELD", "OTHER") } };
        initial = AttackDamageDistributionTests.Spell(initial, "RETURN", "OGN·169/298", "P2");
        initial = AttackDamageDistributionTests.Spell(initial, "REDUCE", "VEN·052", "P2");
        initial = AttackDamageDistributionTests.Spell(initial, "SPARE1", "OGN·048/298", "P1");
        initial = AttackDamageDistributionTests.Spell(initial, "SPARE2", "OGN·048/298", "P1");
        var game = new StandbyDefendObserverTests.Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BATTLEFIELD:BF"));
        while (game.State.StackItems.Count > 0) await game.Top();
        var generation = game.State.CardObjects["UNIT"].ObjectGeneration;

        // A battle opens a genuine response window without P1 playing another card.
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER"));
        if (game.State.FocusPlayerId != "P2")
            await game.Act(game.State.FocusPlayerId!, new PassFocusCommand());
        if (game.State.CardObjects["UNIT"].Power > 3)
        {
            await game.Act("P2", new PlayCardCommand("REDUCE", "VEN·052", ["UNIT"], Mode: "REDUCE_ENEMY"));
            await game.Top();
            if (game.State.FocusPlayerId != "P2")
                await game.Act(game.State.FocusPlayerId!, new PassFocusCommand());
        }
        await game.Act("P2", new PlayCardCommand("RETURN", "OGN·169/298", ["UNIT"]));
        await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.True(game.State.CardObjects["UNIT"].ObjectGeneration > generation);
        await SacredProtectorTests.FinishCombat(game);
        Assert.Equal(1, game.State.PlayerCardsPlayedThisTurn["P1"]);

        var beforeMana = game.State.RunePools["P1"].Mana;
        var beforeHand = game.State.PlayerZones["P1"].Hand.Count;
        var beforeBase = game.State.PlayerZones["P1"].Base.Count;
        await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        Assert.Equal(beforeMana - mana, game.State.RunePools["P1"].Mana);
        Assert.Equal(2, game.State.PlayerCardsPlayedThisTurn["P1"]);
        Assert.Null(game.State.PendingCardChoice);
        while (game.State.StackItems.Count > 0) await game.Top();
        Assert.Equal(beforeHand - 1, game.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal(beforeBase + 1, game.State.PlayerZones["P1"].Base.Count);
        Assert.Equal(initial.CardObjects["UNIT"].Power, game.State.CardObjects["UNIT"].Power);
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["UNIT"].Tags);
        Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
        await RideTheWindCurrentMovementTests.Replay(initial, game);
    }

    // CN 420.4.b / 812.1.c: confirmation is enough even if that other
    // physical card was countered and never successfully resolved.
    [Theory]
    [InlineData("OGN·012/298", 2)]
    [InlineData("OGN·016/298", 3)]
    [InlineData("OGN·020/298", 5)]
    [InlineData("OGN·217/298", 2)]
    [InlineData("OGN·218/298", 3)]
    public async Task ACounteredDifferentCardStillEnablesThePrintedInspireEffect(string no, int mana)
    {
        var initial = VenUnitEntryBasicTests.Position(no);
        initial = AttackDamageDistributionTests.Spell(initial, "FIRST", "OGN·048/298", "P1");
        initial = AttackDamageDistributionTests.Spell(initial, "SPARE1", "OGN·048/298", "P1");
        initial = AttackDamageDistributionTests.Spell(initial, "SPARE2", "OGN·048/298", "P1");
        initial = AttackDamageDistributionTests.Spell(initial, "COUNTER", "OGN·045/298", "P2");
        var game = new VenBasicFlowSpellTests.Driver(initial);
        await game.Act("P1", new PlayCardCommand("FIRST", "OGN·048/298", []));
        var first = Assert.Single(game.State.StackItems).StackItemId;
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·045/298", [first]));
        await game.Top();
        Assert.Empty(game.State.StackItems);
        Assert.DoesNotContain(game.State.CardPlays.Resolved, play => play.Source.ObjectId == "FIRST");
        var beforeMana = game.State.RunePools["P1"].Mana;
        var beforeHand = game.State.PlayerZones["P1"].Hand.Count;
        var beforeBase = game.State.PlayerZones["P1"].Base.Count;
        await game.Act("P1", new PlayCardCommand("UNIT", no,
            no == "OGN·020/298" ? ["SPARE1", "SPARE2"] : [], Destination: "BASE"));
        Assert.Equal(beforeMana - mana, game.State.RunePools["P1"].Mana);
        if (no == "OGN·016/298")
        {
            var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        }
        while (game.State.StackItems.Count > 0) await game.Top();
        if (no == "OGN·020/298")
        {
            Assert.Contains("SPARE1", game.State.PlayerZones["P1"].Graveyard);
            Assert.Contains("SPARE2", game.State.PlayerZones["P1"].Graveyard);
            Assert.Equal(new[] { "P1-DRAW-1", "P1-DRAW-2" }, game.State.PlayerZones["P1"].Hand);
        }
        else Assert.Equal(beforeHand - 1, game.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal(beforeBase + (no == "OGN·218/298" ? 3 : 1), game.State.PlayerZones["P1"].Base.Count);
        if (no == "OGN·217/298")
        {
            Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["UNIT"].Tags);
            Assert.Equal(3, game.State.CardObjects["UNIT"].Power);
        }
        if (no == "OGN·016/298")
        {
            Assert.Equal(initial.CardObjects["ALLY"].Power + 2, game.State.CardObjects["ALLY"].Power);
            await game.Act("P1", new EndTurnCommand());
            Assert.Equal(initial.CardObjects["ALLY"].Power, game.State.CardObjects["ALLY"].Power);
        }
        await game.Replay();
    }
}
