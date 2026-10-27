using Riftbound.Contracts;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class LastBreathDrawBoundaryTests
{
    [Theory]
    [InlineData(0)] [InlineData(2)]
    public async Task EachPaidAgentTextEarnsAnIndependentDiscardTwoThenDrawTwoSkill(int copies)
    {
        const string agent = "OGN·178/298";
        var initial = Add(Position(agent), "P1-RUNE-6", "UNL-R02", "P1", "BASE");
        for (var i = 1; i <= 3; i++) initial = Add(initial, "HELD-" + i, "OGN·219/298", "P1", "HAND");
        for (var i = 4; i <= 7; i++) initial = Add(initial, "P1-DRAW-" + i, "OGN·219/298", "P1", "MAIN_DECK");
        var game = new Driver(initial);
        try
        {
            await Tap(game, "P1"); await game.Act("P1", new PlayCardCommand("UNIT", agent, []));
            Assert.Equal(2, game.State.RunePools["P1"].Mana);
            for (var i = 0; i < copies; i++)
            {
                await game.Act("P1", new RecycleRuneCommand("P1-RUNE-" + i));
                await game.Act("P1", new AssembleEquipmentCommand("SONG-" + i, "UNIT", ["ASSEMBLE_GREEN"]));
                await game.Top(); Assert.Equal(agent, game.State.CardObjects["SONG-" + i].AttachedRuleText!.CardNo);
            }
            await game.Act("P1", new EndTurnCommand()); await Tap(game, "P2");
            await game.Act("P2", new RecycleRuneCommand("P2-RUNE-0"));
            await game.Act("P2", new RecycleRuneCommand("P2-RUNE-1"));
            var prefix = game.Events().Length;
            await game.Act("P2", new PlayCardCommand("KILL", Kill, ["UNIT"])); await game.Top();
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
            Assert.DoesNotContain(game.Events().Skip(prefix), e => e.Event.Kind is "CARD_DISCARDED" or "CARD_DRAWN");
            Assert.Equal(1 + copies, game.State.TriggerQueue.Count + game.State.StackItems.Count);
            if (game.State.TriggerQueue.Count > 0)
                await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            for (var i = 0; i <= copies; i++)
            {
                var hand = game.State.PlayerZones["P1"].Hand.ToArray();
                var deck = game.State.PlayerZones["P1"].MainDeck.ToArray(); prefix = game.Events().Length;
                Assert.Equal(3, hand.Length); Assert.Equal("P1", game.State.StackItems[^1].ControllerId);
                await game.Top(); var choice = Assert.IsType<Riftbound.Engine.PendingHandChoiceState>(game.State.PendingHandChoice);
                Assert.Equal("P1", choice.PlayerId); Assert.Equal(2, choice.RequiredCount); Assert.Equal(2, choice.DrawCount);
                Assert.Equal(hand, choice.LegalObjectIds); Assert.Equal(hand, game.State.PlayerZones["P1"].Hand);
                Assert.DoesNotContain(game.Events().Skip(prefix), e => e.Event.Kind is "CARD_DISCARDED" or "CARD_DRAWN");
                var discarded = hand.Take(2).ToArray();
                await game.Act("P1", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, discarded));
                Assert.Null(game.State.PendingHandChoice);
                Assert.Equal(hand.Skip(2).Concat(deck.Take(2)), game.State.PlayerZones["P1"].Hand);
                Assert.Equal(deck.Skip(2), game.State.PlayerZones["P1"].MainDeck);
                Assert.All(discarded, id => Assert.Contains(id, game.State.PlayerZones["P1"].Graveyard));
                var events = game.Events().Skip(prefix).Select(e => e.Event.Kind).ToArray();
                Assert.Equal(2, events.Count(kind => kind == "CARD_DISCARDED"));
                Assert.True(Array.LastIndexOf(events, "CARD_DISCARDED") < Array.IndexOf(events, "CARD_DRAWN"));
                Assert.Equal(copies - i, game.State.StackItems.Count);
            }
            Assert.Empty(game.State.TriggerQueue); await game.Replay();
        }
        finally { await Save("actual-paid-agent-discard-draw-" + copies, initial, game); }
    }
}
