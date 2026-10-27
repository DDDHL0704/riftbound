using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class LastBreathDrawBoundaryTests
{
    [Theory]
    [InlineData(0)] [InlineData(2)]
    public async Task EachPaidFaerieTextRespondsOnceThenCallsTwoDormantRunesBeforeDrawing(int copies)
    {
        const string faerie = "OGN·075/298";
        var initial = Position(faerie);
        for (var i = 6; i < 9; i++) initial = Add(initial, "P1-RUNE-" + i, "UNL-R02", "P1", "BASE");
        var runeIds = Enumerable.Range(0, 6).Select(i => "FAERIE-RUNE-" + i).ToArray();
        var cards = initial.CardObjects.ToDictionary(); var locations = initial.ObjectLocations.ToDictionary();
        foreach (var id in runeIds)
        {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "UNL-R02", "P1");
            locations[id] = new("P1", "RUNE_DECK");
        }
        initial = initial with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { RuneDeck = runeIds } } };
        var game = new Driver(initial);
        try
        {
            await Tap(game, "P1");
            await game.Act("P1", new PlayCardCommand("UNIT", faerie, []));
            Assert.Equal(2, game.State.RunePools["P1"].Mana);
            Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
            for (var i = 0; i < copies; i++)
            {
                await game.Act("P1", new RecycleRuneCommand("P1-RUNE-" + i));
                await game.Act("P1", new AssembleEquipmentCommand("SONG-" + i, "UNIT", ["ASSEMBLE_GREEN"]));
                await game.Top();
                Assert.Equal(faerie, game.State.CardObjects["SONG-" + i].AttachedRuleText!.CardNo);
            }
            await game.Act("P1", new EndTurnCommand()); await Tap(game, "P2");
            await game.Act("P2", new RecycleRuneCommand("P2-RUNE-0"));
            await game.Act("P2", new RecycleRuneCommand("P2-RUNE-1"));
            var deck = game.State.PlayerZones["P1"].MainDeck.ToArray();
            var runes = game.State.PlayerZones["P1"].RuneDeck.ToArray(); var prefix = game.Events().Length;
            await game.Act("P2", new PlayCardCommand("KILL", Kill, ["UNIT"])); await game.Top();
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
            Assert.Equal(runes, game.State.PlayerZones["P1"].RuneDeck);
            Assert.Empty(game.State.PlayerZones["P1"].Hand);
            Assert.DoesNotContain(game.Events().Skip(prefix), e => e.Event.Kind is "RUNES_CALLED" or "CARD_DRAWN");
            Assert.Equal(1 + copies, game.State.TriggerQueue.Count + game.State.StackItems.Count);
            if (game.State.TriggerQueue.Count > 0)
                await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            for (var i = 0; i <= copies; i++)
            {
                Assert.Equal("P1", game.State.StackItems[^1].ControllerId); prefix = game.Events().Length;
                await game.Top();
                var actual = game.Events().Skip(prefix).Select(e => e.Event).ToArray();
                var call = Assert.Single(actual, e => e.Kind == "RUNES_CALLED");
                var draw = Assert.Single(actual, e => e.Kind == "CARD_DRAWN");
                Assert.Equal(2, Convert.ToInt32(call.Payload["count"]));
                Assert.True(Array.IndexOf(actual, call) < Array.IndexOf(actual, draw));
                Assert.Equal(runes.Skip(2 * (i + 1)), game.State.PlayerZones["P1"].RuneDeck);
                Assert.Equal(deck.Take(i + 1), game.State.PlayerZones["P1"].Hand);
                Assert.All(runes.Take(2 * (i + 1)), id => {
                    Assert.Contains(id, game.State.PlayerZones["P1"].Base);
                    Assert.True(game.State.CardObjects[id].IsExhausted);
                });
                Assert.Equal(copies - i, game.State.StackItems.Count);
            }
            Assert.Empty(game.State.TriggerQueue); await game.Replay();
        }
        finally { await Save("compound-faerie-paid-" + copies, initial, game); }
    }
}
