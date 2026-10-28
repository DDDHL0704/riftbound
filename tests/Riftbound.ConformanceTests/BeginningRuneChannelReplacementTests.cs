using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class BeginningRuneChannelReplacementTests
{
    // The positions deliberately start during turn start, before START. These
    // tests do not claim that either reviewed unit can yet be played normally.
    [Theory]
    [InlineData("VEN·005", 0, 0, 0, 0, 3)]
    [InlineData("VEN·005", 0, 0, 2, 2, 3)]
    [InlineData("VEN·005", 1, 0, 3, 3, 3)]
    [InlineData("VEN·006", 2, 0, 4, 4, 6)]
    [InlineData("VEN·006", 2, 0, 3, 3, 6)]
    [InlineData("VEN·006", 2, 0, 1, 1, 6)]
    [InlineData("VEN·006", 2, 0, 0, 0, 6)]
    [InlineData("VEN·006", 0, 2, 4, 2, 6)]
    public async Task BeginningConditionPrecedesChannelAndReplacementSurvivesRecovery(
        string cardNo, int ownWyverns, int opposingWyverns, int available, int called, int finalMight)
    {
        var state = BeginningRuneConditionTests.Position(cardNo, 0, 1);
        foreach (var (controller, copies, field) in new[] { ("P1", ownWyverns, "F"), ("P2", opposingWyverns, "OTHER") })
            for (var i = 0; i < copies; i++)
                state = OfficialHoldSequenceTests.AddUnit(state, $"{controller}-WYVERN-{i}", "RAD·092/167", field, controller);

        var cards = state.CardObjects.ToDictionary();
        var locations = state.ObjectLocations.ToDictionary();
        foreach (var id in state.PlayerZones["P1"].RuneDeck) { cards.Remove(id); locations.Remove(id); }
        var runeIds = Enumerable.Range(0, available).Select(i => $"CHANNEL-RUNE-{i}").ToArray();
        foreach (var id in runeIds)
        {
            cards[id] = new(id, cardNo: "OGN·007/298", ownerId: "P1", controllerId: "P1",
                isExhausted: true, tags: [CardObjectTags.RuneCard, "COLOR:red"]);
            locations[id] = new("P1", "RUNE_DECK");
        }
        var zones = state.PlayerZones.ToDictionary();
        zones["P1"] = zones["P1"] with { RuneDeck = runeIds };
        state = state with { RoomId = "BEGINNING-RUNE-CHANNEL-REPLACEMENT", CardObjects = cards,
            ObjectLocations = locations, PlayerZones = zones };

        var game = new Driver(state);
        await game.Act("P1", new PassPriorityCommand());
        Assert.Equal(MatchPhases.TurnStart, game.State.Phase);
        Assert.Equal("SCORE", game.State.TurnStartStep);
        Assert.NotNull(Assert.Single(game.State.StackItems).BeginningRune);
        Assert.Equal(available, game.State.PlayerZones["P1"].RuneDeck.Count);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "RUNES_CALLED");

        await game.Top();
        Assert.Equal(MatchPhases.Main, game.State.Phase);
        Assert.Equal(finalMight, game.State.CardObjects["SOURCE"].Power);
        Assert.Empty(game.State.StackItems);
        Assert.Empty(game.State.TriggerQueue);
        var summoned = game.State.PlayerZones["P1"].Base.Where(id => id.StartsWith("CHANNEL-RUNE-", StringComparison.Ordinal)).ToArray();
        Assert.Equal(runeIds.Take(called), summoned);
        Assert.All(summoned.Take(Math.Min(2, called)), id => Assert.False(game.State.CardObjects[id].IsExhausted));
        Assert.All(summoned.Skip(2), id => Assert.True(game.State.CardObjects[id].IsExhausted));
        Assert.All(summoned, id => Assert.Equal("BASE", game.State.ObjectLocations[id].Zone));
        Assert.Equal(called, Convert.ToInt32(Assert.Single(game.Events(), e => e.Event.Kind == "RUNES_CALLED").Event.Payload["count"]));
        await game.Replay();
    }
}
