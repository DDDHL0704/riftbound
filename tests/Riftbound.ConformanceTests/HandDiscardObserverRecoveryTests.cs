using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Run = Riftbound.ConformanceTests.RawEnvelopeTestRun;

namespace Riftbound.ConformanceTests;

public sealed class HandDiscardObserverRecoveryTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ActualJsonRegistryRestoresThePendingSkillAndReplaysTheOriginalNextCommandsExactly()
    {
        var game = await Open(nameof(ActualJsonRegistryRestoresThePendingSkillAndReplaysTheOriginalNextCommandsExactly));
        var frame = Persist(Frame(game)); var journal = new Journal(); var store = new Store(frame);
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal, store);
        var cold = await registry.GetOrCreateAsync(frame.RoomId, default);
        var original = frame.Commands.Last();
        var duplicate = await cold.SubmitAsync(original.PlayerId, original.ClientIntentId,
            GameCommandJsonMapper.Map(original.RawCommand!.Value), original.RawCommand, default);
        Assert.True(duplicate.Accepted); Assert.Empty(journal.Entries);
        Assert.Equal(MatchStateHasher.Hash(game.State), MatchStateHasher.Hash(duplicate.State));
        var prefix = game.Entries.Count;
        await Top(game);
        await MatchOriginalSuffix(cold, game, prefix);
        Assert.False(game.State.CardObjects["JINX"].IsExhausted);
        prefix = game.Entries.Count; await Top(game);
        await MatchOriginalSuffix(cold, game, prefix);
        Assert.Equal(4, journal.Entries.Count); Assert.Equal(1, store.Loads);
        Assert.Equal(1, journal.Entries.SelectMany(e => e.Events).Count(e => e.Kind == "TRIGGER_RESOLVED"
            && Equals(e.Payload.GetValueOrDefault("sourceObjectId"), "JINX")));
        Assert.True((await game.Recovery("original-final")).Replay.IsMatch);
    }

    [Theory]
    [InlineData("source")] [InlineData("batch")]
    public async Task RegistryRejectsCoordinatedFakeDiscardFactsWithoutCachingAndThenLoadsTheRepairedStore(string mutation)
    {
        var game = await Open("discard-registry-" + mutation); var s = game.State;
        var forged = s with { StackItems = s.StackItems.Select(item => item.HandDiscardObserver is not { } c ? item : item with {
            HandDiscardObserver = mutation == "source" ? c with { DiscardSourceObjectId = "JINX" }
                : c with { DiscardedCards = [new("GRAVE", s.CardObjects["GRAVE"].ObjectGeneration)] } }).ToArray() };
        var forgedFrame = Persist(Frame(game, forged));
        // Coordinated current state and all views are structurally legal. Only
        // reconstruction from the unchanged accepted history can establish truth.
        Assert.Empty(MatchRecoveryValidator.Validate(forgedFrame.RoomId, forgedFrame.LastEventSequence,
            forgedFrame.Commands, forgedFrame.Events, forgedFrame.PlayerViews, forgedFrame.AuthoritativeState,
            forgedFrame.CurrentTick, forgedFrame.SpectatorReplayFrame));
        var audit = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(forgedFrame, new CoreRuleEngine(), default);
        Assert.NotEmpty(audit);
        var journal = new Journal(); var store = new Store(forgedFrame);
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal, store);
        for (var n = 0; n < 2; n++)
        {
            var rejected = await Assert.ThrowsAsync<MatchSessionException>(() => registry.GetOrCreateAsync(s.RoomId, default).AsTask());
            Assert.Equal(ErrorCodes.RecoveryInconsistent, rejected.Code);
            Assert.Null(await registry.FindAsync(s.RoomId, default)); Assert.Empty(journal.Entries);
        }
        Assert.Equal(2, store.Loads); store.Current = Persist(Frame(game));
        var cold = await registry.GetOrCreateAsync(s.RoomId, default); Assert.Equal(3, store.Loads);
        var command = Commands(game).Last();
        var duplicate = await cold.SubmitAsync(command.PlayerId, command.ClientIntentId,
            GameCommandJsonMapper.Map(command.RawCommand!.Value), command.RawCommand, default);
        Assert.True(duplicate.Accepted); Assert.Empty(journal.Entries);
        Assert.Equal(MatchStateHasher.Hash(s), MatchStateHasher.Hash(duplicate.State));
    }

    private static async Task MatchOriginalSuffix(IMatchSession cold, Run game, int prefix)
    {
        ResolutionResult? result = null;
        foreach (var command in Commands(game).Skip(prefix))
        {
            result = await cold.SubmitAsync(command.PlayerId, command.ClientIntentId,
                GameCommandJsonMapper.Map(command.RawCommand!.Value), command.RawCommand, default);
            Assert.True(result.Accepted, result.ErrorMessage);
        }
        Assert.NotNull(result); Assert.Equal(MatchStateHasher.Hash(game.State), MatchStateHasher.Hash(result.State));
    }

    private static async Task<Run> Open(string name)
    {
        var run = await Run.Create(name, "P1", "P2");
        var seed = await run.Session.SeedScenarioAsync("P1", "discard-controlled-scenario", "hand-discard-observer", null, default);
        Assert.True(seed.Accepted, seed.ErrorMessage);
        await Act(run, "P1", HandDiscardObserverTests.DiscardCost());
        return run;
    }
    private static async Task Act(Run run, string player, GameCommand command)
    {
        var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
        raw["promptId"] = run.Session.PromptFor(player).PromptId; raw["snapshotTick"] = run.State.Tick;
        var result = await run.Send("discard-" + run.Entries.Count, JsonSerializer.SerializeToElement(raw), player);
        Assert.True(result.Accepted, result.ErrorMessage); FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
    }
    private static async Task Top(Run run)
    {
        await Act(run, run.State.PriorityPlayerId!, new PassPriorityCommand());
        await Act(run, run.State.PriorityPlayerId!, new PassPriorityCommand());
    }
    private static RecoveredCommand[] Commands(Run run) => run.Entries.Select(e =>
        JsonSerializer.Deserialize<RecoveredCommand>(JsonSerializer.Serialize(e, Json), Json)!).ToArray();
    private static MatchRecoveryFrame Frame(Run run, MatchState? authority = null)
    {
        var s = authority ?? run.State; var sequence = run.Entries[^1].CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(s); var prompts = ResolutionResult.BuildPrompts(s);
        var views = s.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, s.Tick, sequence,
            snapshots[id], s.Tick, sequence, prompts[id]));
        var events = run.Entries.SelectMany(e => e.Events.Select((v, i) =>
            new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, v))).ToArray();
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(s.RoomId, s.Tick, sequence, [], s);
        var initial = MatchState.Create(s.RoomId) with { Seed = run.Entries[0].OpeningSeed };
        return new(s.RoomId, s.Tick, sequence, Commands(run), events, views, [], s, initial, spectator)
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
