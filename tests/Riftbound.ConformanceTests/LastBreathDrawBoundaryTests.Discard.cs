using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
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
        var initial = Add(Position(agent), "P1-RUNE-6", "UNL-R05", "P1", "BASE");
        for (var i = 1; i <= 3; i++) initial = Add(initial, "HELD-" + i, "OGN·219/298", "P1", "HAND");
        for (var i = 4; i <= 7; i++) initial = Add(initial, "P1-DRAW-" + i, "OGN·219/298", "P1", "MAIN_DECK");
        var game = new Driver(initial);
        try
        {
            await Tap(game, "P1"); await game.Act("P1", new RecycleRuneCommand("P1-RUNE-6"));
            await game.Act("P1", new PlayCardCommand("UNIT", agent, []));
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
                await game.Top(); var choice = Assert.IsType<PendingHandChoiceState>(game.State.PendingHandChoice);
                Assert.Equal("P1", choice.PlayerId); Assert.Equal(2, choice.RequiredCount); Assert.Equal(2, choice.DrawCount);
                Assert.Equal(hand, choice.LegalObjectIds); Assert.Equal(hand, game.State.PlayerZones["P1"].Hand);
                Assert.DoesNotContain(game.Events().Skip(prefix), e => e.Event.Kind is "CARD_DISCARDED" or "CARD_DRAWN");
                if (i == 0)
                {
                    AssertAgentChoiceBindings(game);
                    foreach (var player in new[] { "P2", "P1" })
                    {
                        var invalid = new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
                            player == "P2" ? hand.Take(2).ToArray() : []);
                        var raw = JsonSerializer.SerializeToNode(invalid, Json)!.AsObject();
                        raw["promptId"] = game.PromptFor(player).PromptId; raw["snapshotTick"] = game.State.Tick;
                        await game.ActRetained(new(player, "agent-choice-reject-" + player,
                            JsonSerializer.SerializeToElement(raw, Json)), expectedAccepted: false);
                    }
                }
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

    private static void AssertAgentChoiceBindings(Driver game)
    {
        var state = game.State; var choice = state.PendingHandChoice!;
        var birth = Assert.IsType<LastBreathDrawContext>(choice.LastBreathDraw);
        foreach (var invalid in new[] {
            choice with { LastBreathDraw = null }, choice with { DrawCount = 1 },
            choice with { LastBreathDraw = birth with { ControllerId = "P2" } },
            choice with { LastBreathDraw = birth with { Source = birth.Source with { Generation = state.CardObjects["UNIT"].ObjectGeneration } } }
        })
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(state with { PendingHandChoice = invalid }),
                error => error.Contains("captured last-breath", StringComparison.Ordinal));
        var commands = game.Commands(); var events = game.Events(); var sequence = commands[^1].CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence, snapshots[p], state.Tick, sequence, prompts[p]));
        var payload = JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(snapshots["P2"].Timing["pendingHandChoice"], Json), Json)!;
        Assert.False(payload.ContainsKey("lastBreathDraw")); Assert.False(payload.ContainsKey("legalObjectIds"));
        payload["lastBreathDraw"] = birth;
        views["P2"] = views["P2"] with { Snapshot = snapshots["P2"] with {
            Timing = new Dictionary<string, object?>(snapshots["P2"].Timing) { ["pendingHandChoice"] = payload } } };
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        Assert.Contains(MatchRecoveryValidator.Validate(state.RoomId, sequence, commands, events, views, state, state.Tick, spectator),
            error => error.Contains("private last-breath draw birth receipt", StringComparison.Ordinal));
    }
}
