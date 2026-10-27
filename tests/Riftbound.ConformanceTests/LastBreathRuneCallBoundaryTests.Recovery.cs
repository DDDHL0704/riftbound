using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class LastBreathRuneCallBoundaryTests
{
    [Fact]
    public async Task ThreeEarnedRuneCallsSurviveTheirSourceActuallyReturningToAPrivateHand()
    {
        var (initial, game, generation) = await Capture(2, privateDraw: true);
        try
        {
            Assert.Equal(3, game.State.TriggerQueue.Count);
            await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            if (game.State.PriorityPlayerId != "P1") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
            await game.Act("P1", new TapRuneCommand("DRAW-RUNE-A")); await game.Act("P1", new TapRuneCommand("DRAW-RUNE-B"));
            await game.Act("P1", new PlayCardCommand("DRAW", "OGN·048/298", [])); await game.Top();
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.True(game.State.CardObjects["UNIT"].ObjectGeneration > generation + 1);
            Assert.Equal(3, game.State.StackItems.Count);
            Assert.All(game.State.StackItems, item => {
                Assert.Equal(generation, item.LastBreathRuneCall!.Source.Generation);
                Assert.Equal("P1", item.LastBreathRuneCall.ControllerId);
            });
            foreach (var view in new[] { ResolutionResult.BuildSnapshots(game.State)["P2"], ResolutionResult.BuildSpectatorSnapshot(game.State) })
            {
                var stack = JsonSerializer.SerializeToElement(view, Json).GetProperty("stack").EnumerateArray().ToArray();
                Assert.Equal(3, stack.Length);
                Assert.All(stack, item => {
                    Assert.Equal("HIDDEN", item.GetProperty("sourceObjectId").GetString());
                    Assert.Equal(TriggerKinds.UnitLastBreathCallRuneOne, item.GetProperty("effectKind").GetString());
                    Assert.False(item.TryGetProperty("lastBreathRuneCall", out _));
                });
            }
            var runes = game.State.PlayerZones["P1"].RuneDeck.ToArray();
            for (var i = 0; i < 3; i++)
            {
                await game.Top(); Assert.Equal(runes.Length - i - 1, game.State.PlayerZones["P1"].RuneDeck.Count);
                Assert.All(runes.Take(i + 1), id => { Assert.Contains(id, game.State.PlayerZones["P1"].Base); Assert.True(game.State.CardObjects[id].IsExhausted); });
            }
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand); Assert.Empty(game.State.StackItems); await game.Replay();
        }
        finally { await SaveRuneRecovery("actual-private-source-three-rune-calls", initial, game); }
    }

    [Fact]
    public async Task RuneBirthBindingsAndPrivatePublicQueuePayloadsRemainStrict()
    {
        var (initial, game, _) = await Capture(2);
        try
        {
            var copied = game.State.TriggerQueue.First(t => t.LastBreathRuneCall?.Origin is not null); var c = copied.LastBreathRuneCall!;
            foreach (var invalid in new[] {
                c with { Source = null! }, c with { Source = c.Source with { ObjectId = null! } },
                c with { Source = c.Source with { Generation = game.State.CardObjects["UNIT"].ObjectGeneration } },
                c with { ControllerId = "UNSEATED" }, c with { InstanceId = null! }, c with { TriggerIndex = -1 },
                c with { Kind = "UNRELATED_EFFECT" }, c with { CreatedAtTick = game.State.Tick + 1 },
                c with { TurnNumber = game.State.TurnNumber + 1 }, c with { Origin = null },
                c with { Origin = c.Origin! with { Equipment = null! } },
                c with { Origin = c.Origin! with { Unit = c.Origin.Unit with { ObjectId = null! } } }
            })
                Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with {
                    TriggerQueue = game.State.TriggerQueue.Select(t => t == copied ? t with { LastBreathRuneCall = invalid } : t).ToArray() }),
                    e => e.Contains("captured last-breath rune call", StringComparison.Ordinal));
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with { TriggerQueue = [..game.State.TriggerQueue, copied] }),
                e => e.Contains("duplicate captured last-breath rune call", StringComparison.Ordinal));

            var commands = game.Commands(); var events = game.Events(); var sequence = commands[^1].CompletedEventSequence;
            var snapshots = ResolutionResult.BuildSnapshots(game.State); var prompts = ResolutionResult.BuildPrompts(game.State);
            var views = game.State.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, game.State.Tick, sequence, snapshots[p], game.State.Tick, sequence, prompts[p]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(game.State.RoomId, game.State.Tick, sequence, [], game.State);
            var queue = JsonSerializer.SerializeToElement(snapshots["P2"].Timing["triggerQueue"], Json).EnumerateArray()
                .Select(item => JsonSerializer.Deserialize<Dictionary<string, object?>>(item.GetRawText(), Json)!).ToArray();
            queue[0]["lastBreathRuneCall"] = c;
            var view = views["P2"]; views["P2"] = view with { Snapshot = view.Snapshot with { Timing = new Dictionary<string, object?>(view.Snapshot.Timing) { ["triggerQueue"] = queue } } };
            Assert.Contains(MatchRecoveryValidator.Validate(game.State.RoomId, sequence, commands, events, views, game.State, game.State.Tick, spectator),
                e => e.Contains("private birth receipt", StringComparison.Ordinal));
        }
        finally { await SaveRuneRecovery("malformed-rune-birth-and-public-queue", initial, game); }
    }

    [Fact]
    public async Task CanonicalRuneBirthStillNeedsItsOriginalDeathControllerAndCreationTick()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var original = PreconstructedDeckCatalog.Build(catalog).Single(d => d.Id == "poppy-standby").Decklist;
        var main = original.MainDeck.Where(no => no != Unit && no != Kill).ToList();
        while (main.Count > 34) main.RemoveAt(main.FindIndex(no => no != original.ChampionCardNo));
        main.InsertRange(0, [Unit, Unit, Unit, Kill, Kill, Kill]);
        var deck = original with { MainDeck = main, RuneDeck = Enumerable.Repeat("UNL-R06", 12).ToArray() };
        Assert.True(OfficialDeckValidator.Validate(deck, catalog).IsValid);
        var initial = MatchReplayInitialStateBuilder.FromSeats("CANONICAL-RUNE-BIRTH", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }) with { Seed = 424242 };
        var game = new VenEmpowerCanonicalOpeningTests.CanonicalGame(initial);
        try
        {
            foreach (var p in new[] { "P1", "P2" }) await game.Act(p, new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields));
            await game.Act("P1", new ReadyCommand()); await game.Act("P2", new ReadyCommand());
            await game.Act(game.State.ActivePlayerId, new MulliganCommand([])); await game.Act(game.State.OpeningSecondActionPlayerId!, new MulliganCommand([]));
            await game.ColdCheckpoint(); await game.WaitFor("P1", 6, Unit, Kill); await game.Tap("P1");
            var unit = game.Hand("P1", Unit).First(); await game.Act("P1", new PlayCardCommand(unit, Unit, []));
            for (var i = 0; i < 2; i++) await game.Act("P1", new RecycleRuneCommand(game.Runes("P1").First()));
            await game.Act("P1", new PlayCardCommand(game.Hand("P1", Kill).First(), Kill, [unit])); await game.Top();
            var earned = Assert.Single(game.State.StackItems); var receipt = Assert.IsType<LastBreathRuneCallContext>(earned.LastBreathRuneCall);
            Assert.Equal("P1", receipt.ControllerId); Assert.False(receipt.SourceWasEmpowered); await game.ColdCheckpoint();
            await game.RejectCoordinatedForgery("rune-birth-death-controller", game.State with { StackItems = [earned with {
                ControllerId = "P2", LastBreathRuneCall = receipt with { ControllerId = "P2" } }] });
            await game.RejectCoordinatedForgeryTwiceThenRecover("rune-birth-creation-tick", game.State with { StackItems = [earned with {
                LastBreathRuneCall = receipt with { CreatedAtTick = receipt.CreatedAtTick - 1 } }] });
            var before = game.State.PlayerZones["P1"].RuneDeck.ToArray(); await game.Top();
            Assert.Equal(before.Length - 1, game.State.PlayerZones["P1"].RuneDeck.Count);
            Assert.Contains(before[0], game.State.PlayerZones["P1"].Base); Assert.True(game.State.CardObjects[before[0]].IsExhausted); await game.ColdCheckpoint();
        }
        finally { await game.Save("last-breath-rune-original-birth-required"); }
    }

    private static async Task SaveRuneRecovery(string label, MatchState initial, Driver game)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_LAST_BREATH_RUNE_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output, label + ".json"), JsonSerializer.Serialize(new {
            scope = "Controlled initial material, real paid copied text/destruction/reaction draw; per-command privacy, recovery, dedup and original action/event replay. Not a canonical opening or external acceptance.",
            initial, final = game.State, commands = game.Commands(), events = game.Events()
        }, Json));
    }
}
