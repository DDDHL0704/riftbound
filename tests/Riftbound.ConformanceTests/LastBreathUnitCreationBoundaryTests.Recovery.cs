using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class LastBreathUnitCreationBoundaryTests
{
    [Fact]
    public async Task CapturedConditionalBirthsRejectMalformedBindingsAndPrivateReceiptsInPublicQueues()
    {
        var (initial, game) = await CaptureBirths();
        try
        {
            var copied = game.State.TriggerQueue.First(t => t.LastBreathUnitCreation?.Origin is not null);
            var c = copied.LastBreathUnitCreation!;
            var malformed = new[] {
                c with { Source = null! }, c with { Source = c.Source with { ObjectId = null! } },
                c with { Source = c.Source with { Generation = game.State.CardObjects["UNIT"].ObjectGeneration } },
                c with { ControllerId = null! }, c with { ControllerId = "UNSEATED" },
                c with { SourceWasEmpowered = false }, c with { TriggerIndex = -1 },
                c with { CreatedAtTick = game.State.Tick + 1 }, c with { TurnNumber = game.State.TurnNumber + 1 },
                c with { InstanceId = null! }, c with { Origin = null },
                c with { Origin = c.Origin! with { Equipment = null! } },
                c with { Origin = c.Origin! with { Unit = c.Origin.Unit with { ObjectId = null! } } }
            };
            foreach (var invalid in malformed)
            {
                var changed = game.State with { TriggerQueue = game.State.TriggerQueue.Select(t => t == copied
                    ? t with { LastBreathUnitCreation = invalid } : t).ToArray() };
                Assert.Contains(OfficialInsightAndSpellLockTests.Errors(changed), e => e.Contains("captured last-breath", StringComparison.Ordinal));
            }
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with { TriggerQueue =
                game.State.TriggerQueue.Select(t => t with { LastBreathUnitCreation = null }).ToArray() }),
                e => e.Contains("captured last-breath", StringComparison.Ordinal));
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with { TriggerQueue = [..game.State.TriggerQueue, copied] }),
                e => e.Contains("duplicate captured last-breath", StringComparison.Ordinal));

            var frame = BirthFrame(initial, game);
            var view = frame.PlayerViews["P2"];
            var queue = JsonSerializer.SerializeToElement(view.Snapshot.Timing["triggerQueue"], Json).EnumerateArray()
                .Select(item => JsonSerializer.Deserialize<Dictionary<string, object?>>(item.GetRawText(), Json)!).ToArray();
            queue[0]["lastBreathUnitCreation"] = c;
            var leaked = view with { Snapshot = view.Snapshot with { Timing = new Dictionary<string, object?>(view.Snapshot.Timing) { ["triggerQueue"] = queue } } };
            var views = new Dictionary<string, RecoveredPlayerView>(frame.PlayerViews) { ["P2"] = leaked };
            Assert.Contains(MatchRecoveryValidator.Validate(frame.RoomId, frame.LastEventSequence, frame.Commands,
                frame.Events, views, frame.AuthoritativeState, frame.CurrentTick, frame.SpectatorReplayFrame),
                e => e.Contains("private birth receipt", StringComparison.Ordinal));
        }
        finally { await Save("malformed-and-public-queue-receipts", initial, game); }
    }

    [Fact]
    public async Task ConsistentCurrentViewsCannotReplaceTheOriginalDeathControllerOrBirthTick()
    {
        var (initial, game) = await CaptureBirths();
        try
        {
            var original = BirthFrame(initial, game); Assert.Empty(original.ValidationErrors);
            foreach (var changeController in new[] { false, true })
            {
                var changed = game.State with { TriggerQueue = game.State.TriggerQueue.Select(t => t with {
                    ControllerId = changeController ? "P2" : t.ControllerId,
                    LastBreathUnitCreation = t.LastBreathUnitCreation! with {
                        ControllerId = changeController ? "P2" : t.ControllerId,
                        CreatedAtTick = changeController ? t.LastBreathUnitCreation.CreatedAtTick : t.LastBreathUnitCreation.CreatedAtTick - 1 }
                }).ToArray() };
                var attempted = BirthFrame(initial, game, changed); Assert.Empty(attempted.ValidationErrors);
                Assert.Equal(MatchStateHasher.HashValue(original.Commands), MatchStateHasher.HashValue(attempted.Commands));
                Assert.Equal(MatchStateHasher.HashValue(original.Events), MatchStateHasher.HashValue(attempted.Events));
                var decoded = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(attempted, Json), Json)!;
                var audit = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(decoded, new CoreRuleEngine(), default);
                Assert.Contains(audit, e => e.Contains("replayed final state hash", StringComparison.Ordinal));
                var store = new BirthStore(decoded); var journal = new BirthJournal();
                var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal, store);
                for (var attempt = 0; attempt < 2; attempt++)
                {
                    var failure = await Assert.ThrowsAsync<MatchSessionException>(() => registry.GetOrCreateAsync(game.State.RoomId, default).AsTask());
                    Assert.Equal(ErrorCodes.RecoveryInconsistent, failure.Code);
                    Assert.Contains("action-log audit failed", failure.Message, StringComparison.Ordinal);
                    Assert.Null(await registry.FindAsync(game.State.RoomId, default)); Assert.Empty(journal.Entries);
                }
                Assert.Equal(2, store.Loads);
                store.Current = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(original, Json), Json)!;
                var cold = await registry.GetOrCreateAsync(game.State.RoomId, default); Assert.Equal(3, store.Loads);
                var last = original.Commands[^1];
                var duplicate = await cold.SubmitAsync(last.PlayerId, last.ClientIntentId,
                    GameCommandJsonMapper.Map(last.RawCommand!.Value), last.RawCommand, default);
                Assert.True(duplicate.Accepted, duplicate.ErrorMessage); Assert.Empty(duplicate.Events); Assert.Empty(journal.Entries);
                Assert.Equal(MatchStateHasher.Hash(game.State), MatchStateHasher.Hash(duplicate.State));
                if (Environment.GetEnvironmentVariable("RIFTBOUND_LAST_BREATH_UNIT_CREATION_EVIDENCE") is { Length: > 0 } output)
                    await File.WriteAllTextAsync(Path.Combine(output, "coordinated-birth-forgery-controller-" + changeController + ".json"),
                        JsonSerializer.Serialize(new { original, attempted = decoded, audit, rejectedLoads = 2, repairedLoads = store.Loads, appended = journal.Entries.Count }, Json));
            }
            await FinishCreations(game); Assert.Equal(6, Minions(game.State).Length); await game.Replay();
        }
        finally { await Save("original-birth-history-required", initial, game); }
    }

    [Fact]
    public async Task ThreeEarnedInstancesRemainPublicSkillsAfterTheirRealSourceReturnsToAPrivateHand()
    {
        var (initial, game) = await CaptureBirths(privateDraw: true);
        try
        {
            var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            Assert.Equal(3, game.State.StackItems.Count);
            if (game.State.PriorityPlayerId != "P1") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
            Assert.Equal(2, game.State.RunePools["P1"].Mana);
            await game.Act("P1", new PlayCardCommand("DRAW", "OGN·048/298", [])); await game.Top();
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
            Assert.True(game.State.CardObjects["UNIT"].ObjectGeneration > generation);
            Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(3, game.State.StackItems.Count);
            foreach (var view in new[] { ResolutionResult.BuildSnapshots(game.State)["P2"], ResolutionResult.BuildSpectatorSnapshot(game.State) })
            {
                var stack = JsonSerializer.SerializeToElement(view, Json).GetProperty("stack").EnumerateArray().ToArray();
                Assert.Equal(3, stack.Length);
                Assert.All(stack, item => {
                    Assert.Equal("HIDDEN", item.GetProperty("sourceObjectId").GetString());
                    Assert.Equal(TriggerKinds.UnitLastBreathCreateMinions, item.GetProperty("effectKind").GetString());
                    Assert.False(item.TryGetProperty("lastBreathUnitCreation", out _));
                });
            }
            await FinishCreations(game); Assert.Equal(6, Minions(game.State).Length);
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand); await game.Replay();
        }
        finally { await Save("three-earned-instances-private-hand", initial, game); }
    }

    private static async Task<(MatchState Initial, Driver Game)> CaptureBirths(bool privateDraw = false)
    {
        var initial = Add(Add(Add(Seed(false), "SONG-A", "SFD·059/221", "P1", "BASE"), "SONG-B", "SFD·059/221", "P1", "BASE"), "EMPOWER-RUNE", "UNL-R06", "P1", "BASE");
        if (privateDraw)
        {
            initial = Add(initial, "DRAW", "OGN·048/298", "P1", "HAND");
            var moved = initial.PlayerZones["P1"].MainDeck.Concat(initial.PlayerZones["P1"].Graveyard).ToArray();
            initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { MainDeck = [], Graveyard = [], Hand = [..initial.PlayerZones["P1"].Hand, ..moved] } },
                ObjectLocations = initial.ObjectLocations.ToDictionary(p => p.Key, p => moved.Contains(p.Key) ? new ObjectLocationState("P1", "HAND") : p.Value) };
        }
        var game = new Driver(initial);
        await Tap(game, "P1"); await game.Act("P1", new RecycleRuneCommand("EMPOWER-RUNE"));
        await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [])); await game.Top();
        Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
        foreach (var (gear, rune) in new[] { ("SONG-A", "P1-RUNE-0"), ("SONG-B", "P1-RUNE-1") })
        {
            await game.Act("P1", new RecycleRuneCommand(rune));
            await game.Act("P1", new AssembleEquipmentCommand(gear, "UNIT", ["ASSEMBLE_GREEN"])); await game.Top();
        }
        await game.Act("P1", new EndTurnCommand()); await Tap(game, "P2");
        await game.Act("P2", new RecycleRuneCommand("P2-RUNE-0")); await game.Act("P2", new RecycleRuneCommand("P2-RUNE-1"));
        await game.Act("P2", new PlayCardCommand("KILL", Kill, ["UNIT"])); await game.Top();
        Assert.Equal(3, game.State.TriggerQueue.Count); Assert.Empty(game.State.StackItems); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
        return (initial, game);
    }

    private static MatchRecoveryFrame BirthFrame(MatchState initial, Driver game, MatchState? changed = null)
    {
        var state = changed ?? game.State; var commands = game.Commands(); var events = game.Events(); var sequence = commands[^1].CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence, snapshots[p], state.Tick, sequence, prompts[p]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        var errors = MatchRecoveryValidator.Validate(state.RoomId, sequence, commands, events, views, state, state.Tick, spectator);
        return new(state.RoomId, state.Tick, sequence, commands, events, views, errors, state, initial, spectator) { OpeningSeed = initial.Seed };
    }
    private sealed class BirthJournal : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
    private sealed class BirthStore(MatchRecoveryFrame current) : IMatchRecoveryStore
    {
        internal MatchRecoveryFrame Current { get; set; } = current;
        internal int Loads { get; private set; }
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string room, CancellationToken token) { Loads++; return ValueTask.FromResult<MatchRecoveryFrame?>(Current); }
    }
}
