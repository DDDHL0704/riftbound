using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class LastBreathDrawBoundaryTests
{
    [Fact]
    public async Task CanonicalFaerieBirthRequiresItsOriginalControllerAndCreationTick()
    {
        const string faerie = "OGN·075/298";
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var fillers = new[] { "OGN·044/298", "OGN·049/298", "OGN·051/298", "OGN·052/298", "OGN·054/298", "OGN·055/298",
            "OGN·056/298", "OGN·059/298", "OGN·061/298", "OGN·065/298", "OGN·070/298" };
        var main = new[] { faerie, faerie, faerie, Kill, Kill, Kill, "OGN·079/298" }
            .Concat(fillers.SelectMany(no => Enumerable.Repeat(no, 3))).ToArray();
        var fields = PreconstructedDeckCatalog.Build(catalog).Single(d => d.Id == "poppy-standby").Decklist.Battlefields;
        var deck = new OfficialDecklist("OGN·261/298", "OGN·079/298", main, Enumerable.Repeat("UNL-R06", 12).ToArray(), fields);
        var legality = OfficialDeckValidator.Validate(deck, catalog); Assert.True(legality.IsValid, string.Join("; ", legality.Errors));
        var initial = MatchReplayInitialStateBuilder.FromSeats("CANONICAL-FAERIE-BIRTH", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }) with { Seed = 424242 };
        var game = new VenEmpowerCanonicalOpeningTests.CanonicalGame(initial);
        try
        {
            foreach (var p in new[] { "P1", "P2" })
                await game.Act(p, new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields));
            await game.Act("P1", new ReadyCommand()); await game.Act("P2", new ReadyCommand());
            await game.Act(game.State.ActivePlayerId, new MulliganCommand([]));
            await game.Act(game.State.OpeningSecondActionPlayerId!, new MulliganCommand([]));
            await game.ColdCheckpoint(); await game.WaitFor("P1", 10, faerie, Kill); await game.Tap("P1");
            var source = game.Hand("P1", faerie).First(); await game.Act("P1", new PlayCardCommand(source, faerie, []));
            for (var i = 0; i < 2; i++) await game.Act("P1", new RecycleRuneCommand(game.Runes("P1").First()));
            await game.Act("P1", new PlayCardCommand(game.Hand("P1", Kill).First(), Kill, [source])); await game.Top();
            var earned = Assert.Single(game.State.StackItems); var birth = Assert.IsType<LastBreathRuneCallContext>(earned.LastBreathRuneCall);
            Assert.Equal(TriggerKinds.UnitLastBreathCallRunesThenDraw, earned.EffectKind); await game.ColdCheckpoint();
            await game.RejectCoordinatedForgery("faerie-birth-death-controller", game.State with { StackItems = [earned with {
                ControllerId = "P2", LastBreathRuneCall = birth with { ControllerId = "P2" } }] });
            await game.RejectCoordinatedForgeryTwiceThenRecover("faerie-birth-creation-tick", game.State with { StackItems = [earned with {
                LastBreathRuneCall = birth with { CreatedAtTick = birth.CreatedAtTick - 1 } }] });
            var runes = game.State.PlayerZones["P1"].RuneDeck.ToArray(); var hand = game.State.PlayerZones["P1"].Hand.Count;
            Assert.True(runes.Length >= 2); await game.Top();
            Assert.Equal(runes.Skip(2), game.State.PlayerZones["P1"].RuneDeck);
            Assert.All(runes.Take(2), id => Assert.True(game.State.CardObjects[id].IsExhausted));
            Assert.Equal(hand + 1, game.State.PlayerZones["P1"].Hand.Count); await game.ColdCheckpoint();
        }
        finally { await game.Save("last-breath-faerie-original-birth-required"); }
    }

    [Fact]
    public async Task TransferredFaerieSkillsKeepDeathControllerForBothInstructionsAfterPrivateReturn()
    {
        var (initial, game, generation) = await CaptureDraws(privateDraw: true, transferControl: true, sourceCardNo: "OGN·075/298");
        try
        {
            Assert.Equal(3, game.State.TriggerQueue.Count);
            Assert.All(game.State.TriggerQueue, t => {
                Assert.Equal("P2", t.ControllerId);
                Assert.Equal(TriggerKinds.UnitLastBreathCallRunesThenDraw, t.EffectKind);
                Assert.Equal(generation, t.LastBreathRuneCall!.Source.Generation);
            });
            await game.Act("P2", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            if (game.State.PriorityPlayerId != "P1") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
            await game.Act("P1", new TapRuneCommand("DRAW-RUNE-A", ["DRAW-RUNE-A", "DRAW-RUNE-B"]));
            await game.Act("P1", new PlayCardCommand("DRAW", "OGN·048/298", [])); await game.Top();
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
            Assert.Equal("P1", game.State.CardObjects["UNIT"].ControllerId);
            Assert.True(game.State.CardObjects["UNIT"].ObjectGeneration > generation + 1);
            Assert.Equal(3, game.State.StackItems.Count);
            foreach (var view in new[] { ResolutionResult.BuildSnapshots(game.State)["P2"], ResolutionResult.BuildSpectatorSnapshot(game.State) })
            {
                var stack = JsonSerializer.SerializeToElement(view, Json).GetProperty("stack").EnumerateArray().ToArray();
                Assert.Equal(3, stack.Length);
                Assert.All(stack, t => {
                    Assert.Equal("HIDDEN", t.GetProperty("sourceObjectId").GetString());
                    Assert.Equal(TriggerKinds.UnitLastBreathCallRunesThenDraw, t.GetProperty("effectKind").GetString());
                    Assert.False(t.TryGetProperty("lastBreathRuneCall", out _));
                });
            }
            var ownerHand = game.State.PlayerZones["P1"].Hand.ToArray();
            var ownerRunes = game.State.PlayerZones["P1"].RuneDeck.ToArray();
            var controllerHand = game.State.PlayerZones["P2"].Hand.Count;
            var controllerRunes = game.State.PlayerZones["P2"].RuneDeck.ToArray();
            Assert.Equal(5, controllerRunes.Length); // Three control payments and two destruction payments were actually recycled.
            var expected = new[] { 2, 2, 1 }; var called = 0;
            for (var i = 0; i < 3; i++)
            {
                Assert.Equal("P2", game.State.StackItems[^1].ControllerId); var prefix = game.Events().Length;
                await game.Top(); var effects = game.Events().Skip(prefix).Select(e => e.Event).ToArray();
                var call = Assert.Single(effects, e => e.Kind == "RUNES_CALLED");
                var draw = Assert.Single(effects, e => e.Kind == "CARD_DRAWN");
                Assert.Equal("P2", call.Payload["playerId"]); Assert.Equal(expected[i], Convert.ToInt32(call.Payload["count"]));
                Assert.Equal("P2", draw.Payload["playerId"]); Assert.True(Array.IndexOf(effects, call) < Array.IndexOf(effects, draw));
                called += expected[i]; Assert.Equal(controllerRunes.Skip(called), game.State.PlayerZones["P2"].RuneDeck);
                Assert.All(controllerRunes.Take(called), id => {
                    Assert.Contains(id, game.State.PlayerZones["P2"].Base); Assert.True(game.State.CardObjects[id].IsExhausted);
                    Assert.Equal("P2", game.State.CardObjects[id].ControllerId);
                });
                Assert.Equal(controllerHand + i + 1, game.State.PlayerZones["P2"].Hand.Count);
                Assert.Equal(ownerHand, game.State.PlayerZones["P1"].Hand); Assert.Equal(ownerRunes, game.State.PlayerZones["P1"].RuneDeck);
                Assert.Equal(2 - i, game.State.StackItems.Count);
            }
            Assert.Empty(game.State.TriggerQueue); await game.Replay();
        }
        finally { await Save("compound-faerie-transferred-private-source", initial, game); }
    }
}
