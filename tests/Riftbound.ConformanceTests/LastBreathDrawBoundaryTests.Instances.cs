using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class LastBreathDrawBoundaryTests
{
    [Theory]
    [InlineData("SFD·036/221", false)] [InlineData("UNL-221/219", false)] [InlineData("UNL-156/219", true)]
    public async Task EveryActuallyCopiedConditionalDrawUsesItsCapturedPreDeparturePosition(string no, bool hasAlly)
    {
        var initial = Position(no);
        if (!hasAlly) initial = initial with {
            CardObjects = initial.CardObjects.Where(p => p.Key != "ALLY").ToDictionary(p => p.Key, p => p.Value),
            ObjectLocations = initial.ObjectLocations.Where(p => p.Key != "ALLY").ToDictionary(p => p.Key, p => p.Value),
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with {
                Base = initial.PlayerZones["P1"].Base.Where(id => id != "ALLY").ToArray() } } };
        var game = new Driver(initial);
        try
        {
            await Tap(game, "P1"); await game.Act("P1", new PlayCardCommand("UNIT", no, []));
            for (var i = 0; i < 2; i++) {
                await game.Act("P1", new RecycleRuneCommand("P1-RUNE-" + i));
                await game.Act("P1", new AssembleEquipmentCommand("SONG-" + i, "UNIT", ["ASSEMBLE_GREEN"])); await game.Top();
            }
            await game.Act("P1", new EndTurnCommand()); await Tap(game, "P2");
            await game.Act("P2", new RecycleRuneCommand("P2-RUNE-0")); await game.Act("P2", new RecycleRuneCommand("P2-RUNE-1"));
            var deck = game.State.PlayerZones["P1"].MainDeck.ToArray();
            await game.Act("P2", new PlayCardCommand("KILL", Kill, ["UNIT"])); await game.Top();
            Assert.Empty(game.State.PlayerZones["P1"].Hand); Assert.Equal(3, game.State.TriggerQueue.Count);
            Assert.All(game.State.TriggerQueue, item => Assert.Equal(!hasAlly, item.LastBreathDraw!.SourceWasAlone));
            Assert.Equal(3, game.State.TriggerQueue.Select(item => item.LastBreathDraw!.InstanceId).Distinct().Count());
            await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            for (var i = 0; i < 3; i++) { await game.Top(); Assert.Equal(deck.Take(i + 1), game.State.PlayerZones["P1"].Hand); }
            Assert.Empty(game.State.StackItems); await game.Replay();
        }
        finally { await Save("paid-conditional-copies-" + no.Replace('/', '-'), initial, game); }
    }

    [Theory]
    [InlineData("UNL-172/219")] [InlineData("UNL-172a/219")]
    public async Task RealEphemeralGrantAndNaturalBeginningGiveEachCopiedLeblancTwoDraws(string no)
    {
        var initial = Add(Position(no), "EPHEMERAL", "UNL-165/219", "P1", "HAND");
        for (var i = 6; i < 10; i++) initial = Add(initial, "P1-RUNE-" + i, "UNL-R06", "P1", "BASE");
        for (var i = 4; i <= 12; i++) initial = Add(initial, "P1-DRAW-" + i, "OGN·219/298", "P1", "MAIN_DECK");
        var game = new Driver(initial);
        try
        {
            await Tap(game, "P1"); await game.Act("P1", new RecycleRuneCommand("P1-RUNE-2"));
            await game.Act("P1", new PlayCardCommand("UNIT", no, []));
            for (var i = 0; i < 2; i++) {
                await game.Act("P1", new RecycleRuneCommand("P1-RUNE-" + i));
                await game.Act("P1", new AssembleEquipmentCommand("SONG-" + i, "UNIT", ["ASSEMBLE_GREEN"])); await game.Top();
            }
            await game.Act("P1", new PlayCardCommand("EPHEMERAL", "UNL-165/219", ["UNIT"])); await game.Top();
            Assert.Contains(CardObjectTags.Ephemeral, game.State.CardObjects["UNIT"].GrantedTags ?? []);
            Assert.Equal(2, game.State.PlayerZones["P1"].Hand.Count);
            await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
            Assert.Equal(MatchPhases.TurnStart, game.State.Phase); Assert.NotNull(Assert.Single(game.State.StackItems).EphemeralStart);
            await game.Top(); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
            Assert.Equal(3, game.State.TriggerQueue.Count); Assert.Equal(2, game.State.PlayerZones["P1"].Hand.Count);
            await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            for (var i = 0; i < 2; i++) { await game.Top(); Assert.Equal(4 + 2 * i, game.State.PlayerZones["P1"].Hand.Count); }
            await game.Top();
            for (var step = 0; game.State.Phase == MatchPhases.TurnStart; step++) {
                Assert.InRange(step, 0, 8); await game.Act(game.State.PriorityPlayerId ?? game.State.TurnPlayerId!, new PassPriorityCommand());
            }
            Assert.Equal(9, game.State.PlayerZones["P1"].Hand.Count); // two granted draws, six last-breath draws and one normal turn draw
            Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue); await game.Replay();
        }
        finally { await Save("paid-copied-leblanc-natural-beginning-" + no.Replace('/', '-'), initial, game); }
    }

    [Fact]
    public async Task TransferredDrawsRemainWithDeathControllerAfterRealReturnToOwnersHand()
    {
        var (initial, game, generation) = await CaptureDraws(privateDraw: true, transferControl: true);
        try
        {
            Assert.Equal(3, game.State.TriggerQueue.Count);
            Assert.All(game.State.TriggerQueue, item => Assert.Equal("P2", item.LastBreathDraw!.ControllerId));
            await game.Act("P2", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            if (game.State.PriorityPlayerId != "P1") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
            await game.Act("P1", new TapRuneCommand("DRAW-RUNE-A")); await game.Act("P1", new TapRuneCommand("DRAW-RUNE-B"));
            await game.Act("P1", new PlayCardCommand("DRAW", "OGN·048/298", [])); await game.Top();
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand); Assert.Equal("P1", game.State.CardObjects["UNIT"].ControllerId);
            Assert.True(game.State.CardObjects["UNIT"].ObjectGeneration > generation + 1);
            Assert.All(game.State.StackItems, item => { Assert.Equal("P2", item.ControllerId); Assert.Equal(generation, item.LastBreathDraw!.Source.Generation); });
            foreach (var view in new[] { ResolutionResult.BuildSnapshots(game.State)["P2"], ResolutionResult.BuildSpectatorSnapshot(game.State) })
            {
                var stack = JsonSerializer.SerializeToElement(view, Json).GetProperty("stack").EnumerateArray().ToArray(); Assert.Equal(3, stack.Length);
                Assert.All(stack, item => {
                    Assert.Equal("HIDDEN", item.GetProperty("sourceObjectId").GetString());
                    Assert.Equal(TriggerKinds.UnitLastBreathDrawOne, item.GetProperty("effectKind").GetString());
                    Assert.False(item.TryGetProperty("lastBreathDraw", out _));
                });
            }
            var ownerHand = game.State.PlayerZones["P1"].Hand.ToArray(); var controllerHand = game.State.PlayerZones["P2"].Hand.Count;
            for (var i = 0; i < 3; i++) {
                await game.Top(); Assert.Equal(2 - i, game.State.StackItems.Count);
                Assert.Equal(ownerHand, game.State.PlayerZones["P1"].Hand); Assert.Equal(controllerHand + i + 1, game.State.PlayerZones["P2"].Hand.Count);
            }
            await game.Replay();
        }
        finally { await Save("actual-transferred-private-source-three-draws", initial, game); }
    }
}
