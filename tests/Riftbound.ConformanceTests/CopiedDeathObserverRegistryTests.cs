using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Run = Riftbound.ConformanceTests.RawEnvelopeTestRun;

namespace Riftbound.ConformanceTests;

public sealed class CopiedDeathObserverRegistryTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task JsonRegistryRestoresBothInstancesAndMatchesOriginalCommandsAndEvents()
    {
        var game = await Open(nameof(JsonRegistryRestoresBothInstancesAndMatchesOriginalCommandsAndEvents));
        var frame = Persist(Frame(game)); game.Save("pending-frame", frame);
        var journal = new Journal(); var store = new Store(frame);
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal, store);
        var cold = await registry.GetOrCreateAsync(frame.RoomId, default);
        var original = frame.Commands.Last();
        var duplicate = await cold.SubmitAsync(original.PlayerId, original.ClientIntentId,
            GameCommandJsonMapper.Map(original.RawCommand!.Value), original.RawCommand, default);
        Assert.True(duplicate.Accepted, duplicate.ErrorMessage); Assert.Empty(journal.Entries);
        Assert.Equal(MatchStateHasher.Hash(game.State), MatchStateHasher.Hash(duplicate.State));
        var prefix = game.Entries.Count;
        await Act(game, "P2", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        await Top(game); await Top(game);
        foreach (var entry in game.Entries.Skip(prefix))
        {
            var command = Command(entry);
            var result = await cold.SubmitAsync(command.PlayerId, command.ClientIntentId,
                GameCommandJsonMapper.Map(command.RawCommand!.Value), command.RawCommand, default);
            Assert.True(result.Accepted, result.ErrorMessage);
            Assert.Equal(MatchStateHasher.Hash(entry.AuthoritativeState), MatchStateHasher.Hash(result.State));
            Assert.Equal(MatchStateHasher.HashValue(entry.Events), MatchStateHasher.HashValue(result.Events));
        }
        Assert.Equal(1, store.Loads); Assert.Equal(5, journal.Entries.Count);
        Assert.Equal(2, game.State.PlayerExperience["P2"]);
        Assert.Equal(2, journal.Entries.SelectMany(e => e.Events).Count(e => e.Kind == "TRIGGER_RESOLVED"
            && Equals(e.Payload.GetValueOrDefault("sourceObjectId"), "HOST")));
        var proof = await game.Recovery("original-final");
        Assert.Empty(proof.Errors); Assert.Empty(proof.AuditErrors); Assert.True(proof.Replay.IsMatch);
        Assert.Null(proof.RegistryFailure);
        game.Save("cold-journal", journal.Entries);
    }

    [Fact]
    public async Task RegistryRejectsCoordinatedFakeDeathWithoutCachingAndThenLoadsTheRepairedStore()
    {
        var game = await Open(nameof(RegistryRejectsCoordinatedFakeDeathWithoutCachingAndThenLoadsTheRepairedStore));
        var s = game.State;
        var forged = s with { TriggerQueue = s.TriggerQueue.Select(t => t.DeathObserver?.Origin is null ? t : t with {
            DeathObserver = t.DeathObserver with { Destroyed = t.DeathObserver.Destroyed with { ObjectId = "GRAVE" } } }).ToArray() };
        var fake = Persist(Frame(game, forged)); game.Save("coordinated-fake-frame", fake);
        Assert.Empty(MatchRecoveryValidator.Validate(fake.RoomId, fake.LastEventSequence, fake.Commands,
            fake.Events, fake.PlayerViews, fake.AuthoritativeState, fake.CurrentTick, fake.SpectatorReplayFrame));
        var audit = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(fake, new CoreRuleEngine(), default);
        Assert.NotEmpty(audit); game.Save("coordinated-fake-audit", audit);
        var journal = new Journal(); var store = new Store(fake);
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal, store);
        for (var i = 0; i < 2; i++)
        {
            var ex = await Assert.ThrowsAsync<MatchSessionException>(() => registry.GetOrCreateAsync(s.RoomId, default).AsTask());
            Assert.Equal(ErrorCodes.RecoveryInconsistent, ex.Code);
            Assert.Null(await registry.FindAsync(s.RoomId, default)); Assert.Empty(journal.Entries);
        }
        Assert.Equal(2, store.Loads); store.Current = Persist(Frame(game));
        var cold = await registry.GetOrCreateAsync(s.RoomId, default); Assert.Equal(3, store.Loads);
        var original = Command(game.Entries.Last());
        var duplicate = await cold.SubmitAsync(original.PlayerId, original.ClientIntentId,
            GameCommandJsonMapper.Map(original.RawCommand!.Value), original.RawCommand, default);
        Assert.True(duplicate.Accepted, duplicate.ErrorMessage); Assert.Empty(journal.Entries);
        Assert.Equal(MatchStateHasher.Hash(s), MatchStateHasher.Hash(duplicate.State));
        game.Save("registry-rejection-repair", new { rejectedLoads = 2, totalLoads = store.Loads,
            appendedCommands = journal.Entries.Count, duplicateAccepted = duplicate.Accepted,
            restoredHash = MatchStateHasher.Hash(duplicate.State) });
    }

    private static async Task<Run> Open(string name)
    {
        var game = await Run.Create(name, "P1", "P2");
        var seeded = await game.Session.SeedScenarioAsync("P1", "copied-death-controlled-scenario", "copied-death-observer", null, default);
        Assert.True(seeded.Accepted, seeded.ErrorMessage);
        await Act(game, "P2", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_GREEN"])); await Top(game);
        await Act(game, "P2", new EndTurnCommand()); await Act(game, "P1", new TapRuneCommand("PAY"));
        await Act(game, "P1", new PlayCardCommand("AOE", "OGN·133/298", [])); await Top(game);
        game.Save("original-pending-journal", game.Entries);
        Assert.Contains("D", game.State.PlayerZones["P2"].Graveyard);
        Assert.Equal(2, game.State.TriggerQueue.Count);
        Assert.Single(game.State.TriggerQueue, t => t.DeathObserver!.Origin is not null);
        return game;
    }

    private static async Task Act(Run game, string player, GameCommand command)
    {
        var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
        raw["promptId"] = game.Session.PromptFor(player).PromptId; raw["snapshotTick"] = game.State.Tick;
        var result = await game.Send("copied-death-" + game.Entries.Count, JsonSerializer.SerializeToElement(raw), player);
        Assert.True(result.Accepted, result.ErrorMessage); FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
    }
    private static async Task Top(Run game)
    {
        await Act(game, game.State.PriorityPlayerId!, new PassPriorityCommand());
        await Act(game, game.State.PriorityPlayerId!, new PassPriorityCommand());
    }
    private static RecoveredCommand Command(MatchJournalEntry e)
        => JsonSerializer.Deserialize<RecoveredCommand>(JsonSerializer.Serialize(e, Json), Json)!;
    private static MatchRecoveryFrame Frame(Run game, MatchState? authority = null)
    {
        var s = authority ?? game.State; var sequence = game.Entries[^1].CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(s); var prompts = ResolutionResult.BuildPrompts(s);
        var views = s.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, s.Tick, sequence,
            snapshots[id], s.Tick, sequence, prompts[id]));
        var events = game.Entries.SelectMany(e => e.Events.Select((v, i) =>
            new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, v))).ToArray();
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(s.RoomId, s.Tick, sequence, [], s);
        var initial = MatchState.Create(s.RoomId) with { Seed = game.Entries[0].OpeningSeed };
        return new(s.RoomId, s.Tick, sequence, game.Entries.Select(Command).ToArray(), events, views, [], s, initial, spectator)
            { OpeningSeed = initial.Seed };
    }
    private static MatchRecoveryFrame Persist(MatchRecoveryFrame frame)
        => JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
    private sealed class Journal : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
    private sealed class Store(MatchRecoveryFrame frame) : IMatchRecoveryStore
    {
        internal MatchRecoveryFrame Current { get; set; } = frame;
        internal int Loads { get; private set; }
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string room, CancellationToken token)
        { Loads++; return ValueTask.FromResult<MatchRecoveryFrame?>(Current); }
    }
}
