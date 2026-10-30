using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class LastBreathDrawBoundaryTests
{
    [Fact]
    public async Task EarnedDrawsKeepTheirPublicSkillsAfterSourceActuallyReturnsToAPrivateHand()
    {
        var (initial, game, generation) = await CaptureDraws(privateDraw: true);
        try
        {
            Assert.Equal(3, game.State.TriggerQueue.Count);
            await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            if (game.State.PriorityPlayerId != "P1") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
            await game.Act("P1", new TapRuneCommand("DRAW-RUNE-A")); await game.Act("P1", new TapRuneCommand("DRAW-RUNE-B"));
            await game.Act("P1", new PlayCardCommand("DRAW", "OGN·048/298", [])); await game.Top();
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
            Assert.True(game.State.CardObjects["UNIT"].ObjectGeneration > generation + 1);
            Assert.Equal(3, game.State.StackItems.Count);
            Assert.All(game.State.StackItems, item => Assert.Equal(generation, item.LastBreathDraw!.Source.Generation));
            foreach (var view in new[] { ResolutionResult.BuildSnapshots(game.State)["P2"], ResolutionResult.BuildSpectatorSnapshot(game.State) })
            {
                var stack = JsonSerializer.SerializeToElement(view, Json).GetProperty("stack").EnumerateArray().ToArray();
                Assert.Equal(3, stack.Length);
                Assert.All(stack, item => {
                    Assert.Equal("HIDDEN", item.GetProperty("sourceObjectId").GetString());
                    Assert.Equal(TriggerKinds.UnitLastBreathDrawOne, item.GetProperty("effectKind").GetString());
                    Assert.False(item.TryGetProperty("lastBreathDraw", out _));
                });
            }
            for (var i = 0; i < 3; i++)
            {
                await game.Top(); Assert.Equal(2 - i, game.State.StackItems.Count);
                Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
            }
            Assert.Empty(game.State.TriggerQueue); await game.Replay();
        }
        finally { await Save("actual-private-source-three-draws", initial, game); }
    }

    [Fact]
    public async Task DrawBirthBindingsAndPublicQueueReceiptsRemainStrict()
    {
        var (initial, game, _) = await CaptureDraws();
        try
        {
            Assert.Empty(OfficialInsightAndSpellLockTests.Errors(game.State));
            var copied = game.State.TriggerQueue.First(t => t.LastBreathDraw?.Origin is not null); var c = copied.LastBreathDraw!;
            foreach (var invalid in new[] {
                c with { Source = null! }, c with { Source = c.Source with { ObjectId = null! } },
                c with { Source = c.Source with { Generation = game.State.CardObjects["UNIT"].ObjectGeneration } },
                c with { CardNo = null! }, c with { ControllerId = "UNSEATED" }, c with { InstanceId = null! },
                c with { TriggerIndex = -1 }, c with { Kind = "UNRELATED_EFFECT" }, c with { CreatedAtTick = game.State.Tick + 1 },
                c with { TurnNumber = game.State.TurnNumber + 1 }, c with { Origin = null },
                c with { Origin = c.Origin! with { Equipment = null! } },
                c with { Origin = c.Origin! with { Unit = c.Origin.Unit with { ObjectId = null! } } }
            })
                Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with {
                    TriggerQueue = game.State.TriggerQueue.Select(t => t == copied ? t with { LastBreathDraw = invalid } : t).ToArray() }),
                    e => e.Contains("captured last-breath draw", StringComparison.Ordinal));
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with { TriggerQueue = [..game.State.TriggerQueue, copied] }),
                e => e.Contains("duplicate captured last-breath draw", StringComparison.Ordinal));

            var commands = game.Commands(); var events = game.Events(); var sequence = commands[^1].CompletedEventSequence;
            var snapshots = ResolutionResult.BuildSnapshots(game.State); var prompts = ResolutionResult.BuildPrompts(game.State);
            var views = game.State.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, game.State.Tick, sequence, snapshots[p], game.State.Tick, sequence, prompts[p]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(game.State.RoomId, game.State.Tick, sequence, [], game.State);
            var queue = JsonSerializer.SerializeToElement(snapshots["P2"].Timing["triggerQueue"], Json).EnumerateArray()
                .Select(item => JsonSerializer.Deserialize<Dictionary<string, object?>>(item.GetRawText(), Json)!).ToArray();
            queue[0]["lastBreathDraw"] = c;
            var view = views["P2"]; views["P2"] = view with { Snapshot = view.Snapshot with { Timing = new Dictionary<string, object?>(view.Snapshot.Timing) { ["triggerQueue"] = queue } } };
            Assert.Contains(MatchRecoveryValidator.Validate(game.State.RoomId, sequence, commands, events, views, game.State, game.State.Tick, spectator),
                e => e.Contains("private birth receipt", StringComparison.Ordinal));
        }
        finally { await Save("malformed-draw-birth-and-public-queue", initial, game); }
    }

    [Fact]
    public async Task ConsistentCurrentDrawViewsStillNeedTheOriginalDeathControllerAndBirthTick()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        // The sentinel is blue and Vengeance yellow; the previous Poppy
        // template did not admit blue. Retain an actual legal submitted deck.
        var fillers = new[] { "OGN·087/298", "OGN·103/298", "OGN·116/298", "OGN·210/298", "OGN·216/298", "OGN·217/298",
            "SFD·065/221", "SFD·069/221", "SFD·085/221", "SFD·155/221", "SFD·159/221" };
        var main = new[] { Unit, Unit, Unit, Kill, Kill, Kill, "OGN·246/298" }
            .Concat(fillers.SelectMany(no => Enumerable.Repeat(no, 3))).ToArray();
        var fields = PreconstructedDeckCatalog.Build(catalog).Single(d => d.Id == "poppy-standby").Decklist.Battlefields;
        var deck = new OfficialDecklist("OGN·265/298", "OGN·246/298", main, Enumerable.Repeat("UNL-R06", 12).ToArray(), fields);
        var legality = OfficialDeckValidator.Validate(deck, catalog); Assert.True(legality.IsValid, string.Join("; ", legality.Errors));
        var initial = MatchReplayInitialStateBuilder.FromSeats("CANONICAL-DRAW-BIRTH", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }) with { Seed = 424242 };
        var game = new VenEmpowerCanonicalOpeningTests.CanonicalGame(initial);
        try
        {
            foreach (var p in new[] { "P1", "P2" }) await game.Act(p, new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields));
            await game.Act("P1", new ReadyCommand()); await game.Act("P2", new ReadyCommand());
            await game.Act(game.State.ActivePlayerId, new MulliganCommand([])); await game.Act(game.State.OpeningSecondActionPlayerId!, new MulliganCommand([]));
            await game.ColdCheckpoint(); await game.WaitFor("P1", 6, Unit, Kill); await game.Tap("P1");
            var source = game.Hand("P1", Unit).First(); await game.Act("P1", new PlayCardCommand(source, Unit, []));
            for (var i = 0; i < 2; i++) await game.Act("P1", new RecycleRuneCommand(game.Runes("P1").First()));
            await game.Act("P1", new PlayCardCommand(game.Hand("P1", Kill).First(), Kill, [source])); await game.Top();
            var earned = Assert.Single(game.State.StackItems); var receipt = Assert.IsType<LastBreathDrawContext>(earned.LastBreathDraw);
            Assert.Equal("P1", receipt.ControllerId); await game.ColdCheckpoint();
            await game.RejectCoordinatedForgery("draw-birth-death-controller", game.State with { StackItems = [earned with {
                ControllerId = "P2", LastBreathDraw = receipt with { ControllerId = "P2" } }] });
            await game.RejectCoordinatedForgeryTwiceThenRecover("draw-birth-creation-tick", game.State with { StackItems = [earned with {
                LastBreathDraw = receipt with { CreatedAtTick = receipt.CreatedAtTick - 1 } }] });
            var before = game.State.PlayerZones["P1"].Hand.Count; await game.Top();
            Assert.Equal(before + 1, game.State.PlayerZones["P1"].Hand.Count); await game.ColdCheckpoint();
        }
        finally { await game.Save("last-breath-draw-original-birth-required"); }
    }

    internal static async Task<(MatchState Initial, Driver Game, long Generation)> CaptureDraws(bool privateDraw = false, bool transferControl = false, string sourceCardNo = Unit)
    {
        var initial = Position(sourceCardNo);
        if (transferControl)
        {
            initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { Hand = initial.PlayerZones["P1"].Hand.Where(id => id != "UNIT").ToArray(),
                    Battlefields = [..initial.PlayerZones["P1"].Battlefields, "UNIT"] } },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["UNIT"] = new("P1", "BATTLEFIELD", "BF") } };
            initial = Add(initial, "CONTROL", "OGN·203/298", "P2", "HAND");
            for (var i = 0; i < 8; i++) initial = Add(initial, "CONTROL-RUNE-" + i, i < 3 ? "UNL-R05" : "UNL-R06", "P2", "BASE");
        }
        if (privateDraw)
        {
            initial = Add(Add(Add(initial, "DRAW", "OGN·048/298", "P1", "HAND"), "DRAW-RUNE-A", "UNL-R06", "P1", "BASE"), "DRAW-RUNE-B", "UNL-R06", "P1", "BASE");
            var moved = initial.PlayerZones["P1"].MainDeck.Concat(initial.PlayerZones["P1"].Graveyard).ToArray();
            initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { MainDeck = [], Graveyard = [], Hand = [..initial.PlayerZones["P1"].Hand, ..moved] } },
                ObjectLocations = initial.ObjectLocations.ToDictionary(p => p.Key, p => moved.Contains(p.Key) ? new ObjectLocationState("P1", "HAND") : p.Value) };
            if (!transferControl)
            {
                // Fixed seed 424242 draws the real departed UNIT first and
                // leaves three recyclable cards for the three earned draws.
                foreach (var id in new[] { "RESERVE-DRAW-2", "RESERVE-DRAW-3", "RESERVE-DRAW-4" })
                    initial = Add(initial, id, "OGN·219/298", "P1", "GRAVEYARD");
            }
        }
        var game = new Driver(initial);
        // Reserve the two reaction-draw runes until P2's turn.
        await game.TapRunes("P1", Enumerable.Range(0, 6).Select(i => "P1-RUNE-" + i));
        if (!transferControl) await game.Act("P1", new PlayCardCommand("UNIT", sourceCardNo, []));
        var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
        for (var i = 0; i < 2; i++) {
            await game.Act("P1", new RecycleRuneCommand("P1-RUNE-" + i));
            await game.Act("P1", new AssembleEquipmentCommand("SONG-" + i, "UNIT", ["ASSEMBLE_GREEN"])); await game.Top();
        }
        await game.Act("P1", new EndTurnCommand()); await Tap(game, "P2");
        if (transferControl)
        {
            for (var i = 0; i < 3; i++) await game.Act("P2", new RecycleRuneCommand("CONTROL-RUNE-" + i));
            await game.Act("P2", new PlayCardCommand("CONTROL", "OGN·203/298", ["UNIT"])); await game.Top();
            Assert.Equal("P2", game.State.CardObjects["UNIT"].ControllerId);
            Assert.Equal(generation, game.State.CardObjects["UNIT"].ObjectGeneration);
        }
        await game.Act("P2", new RecycleRuneCommand("P2-RUNE-0")); await game.Act("P2", new RecycleRuneCommand("P2-RUNE-1"));
        await game.Act("P2", new PlayCardCommand("KILL", Kill, ["UNIT"])); await game.Top();
        return (initial, game, generation);
    }
}
